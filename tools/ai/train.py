"""AI 난이도 학습기: 시뮬레이터(tools/TacticsSim)가 쓴 결정 기록으로 후보 점수 모델을 학습하고 텍스트 모델 파일로 내보낸다.

명령:
  python tools/ai/train.py bc   --games 1000 --out tools/ai/runs/bc.txt          규칙 AI 모방 학습(초기 정책)
  python tools/ai/train.py ppo  --init tools/ai/runs/bc.txt --iters 200         자기 대국 PPO, 스냅샷은 tools/ai/runs/ppo_XXXX.txt
  python tools/ai/train.py eval --a A --b B --games 200                          A의 승·패·무(진영 교대)
모델 형식·결정 지점·특징: PolicySystem.cs, docs/spec/csv/ai-difficulty.md#모델-파일.
"""
import argparse
import glob
import json
import os
import random
import struct
import subprocess
import sys
import tempfile
import time
from concurrent.futures import ThreadPoolExecutor

import numpy as np
import torch
import torch.nn as nn

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SIM = os.path.join(ROOT, 'tools', 'TacticsSim', 'bin', 'Release', 'net8.0', 'TacticsSim.dll')
RUNS = os.path.join(ROOT, 'tools', 'ai', 'runs')
G, K, C = 20, 5, 20  # PolicySystem.GlobalSize, KindCount, CandidateSize
IN = G + K + C
HIDDEN = (64, 64)
WORKERS = max(1, (os.cpu_count() or 2) - 1)
torch.set_num_threads(max(1, (os.cpu_count() or 2) // 2))


# ---------------- 시뮬레이터 ----------------

def sim(*args):
    out = subprocess.run(['dotnet', SIM, *map(str, args)], capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError(f'sim {args[:1]} failed: {out.stderr[-2000:]}')
    return out.stdout


def read_rollout(path):
    """결정 기록 파일 → 게임 목록. 각 게임: dict(seed, winner, turns, score=(P,E), dec=[(team, turn, kind, chosen, logp, global, cands)])."""
    data = open(path, 'rb').read()
    magic, n, g, c = struct.unpack_from('<iiii', data, 0)
    assert magic == 0x50544731 and g == G and c == C, 'trace format'
    off = 16
    games = []
    for _ in range(n):
        seed, winner, turns, sp, se, nd = struct.unpack_from('<iiiiii', data, off)
        off += 24
        dec = []
        for _ in range(nd):
            team, turn, kind, ncand, chosen, logp = struct.unpack_from('<BhBhif', data, off)
            off += 14
            glob_ = np.frombuffer(data, '<f4', G, off); off += 4 * G
            cands = np.frombuffer(data, '<f4', ncand * C, off).reshape(ncand, C); off += 4 * ncand * C
            dec.append((team, turn, kind, chosen, logp, glob_, cands))
        games.append(dict(seed=seed, winner=winner, turns=turns, score=(sp, se), dec=dec))
    return games


def run_rollouts(jobs):
    """jobs: [(games, seed, player, enemy, record, temp)] → 게임 목록(병렬)."""
    tmp = tempfile.mkdtemp(prefix='rollout_')

    def one(i_job):
        i, (games, seed, player, enemy, record, temp) = i_job
        out = os.path.join(tmp, f'{i}.bin')
        sim('rollout', games, seed, out, player, enemy, record, temp)
        res = read_rollout(out)
        os.remove(out)
        for gm in res:
            gm['record'] = record
        return res

    with ThreadPoolExecutor(WORKERS) as ex:
        results = list(ex.map(one, enumerate(jobs)))
    os.rmdir(tmp)
    return [g for r in results for g in r]


def evaluate(a, b, games, seed=900000, ta=0.0, ba=0.0, tb=0.0, bb=0.0):
    """a 대 b 승·패·무(진영 교대). 판을 WORKERS 몫으로 나눈다."""
    per = max(2, (games // WORKERS) // 2 * 2)
    seeds = list(range(seed, seed + games // 2, per // 2))
    def one(s):
        return list(map(int, sim('eval', per, s, a, b, ta, ba, tb, bb).split()))
    with ThreadPoolExecutor(WORKERS) as ex:
        res = list(ex.map(one, seeds))
    w = sum(r[0] for r in res); l = sum(r[1] for r in res); d = sum(r[2] for r in res)
    return w, l, d


# ---------------- 모델 ----------------

class Scorer(nn.Module):
    def __init__(self, mean, std):
        super().__init__()
        self.register_buffer('mean', torch.tensor(mean, dtype=torch.float32))
        self.register_buffer('std', torch.tensor(std, dtype=torch.float32))
        layers, n = [], IN
        for h in HIDDEN:
            layers += [nn.Linear(n, h), nn.ReLU()]
            n = h
        layers.append(nn.Linear(n, 1))
        self.net = nn.Sequential(*layers)

    def forward(self, x):
        return self.net((x - self.mean) / self.std).squeeze(-1)


class Value(nn.Module):
    def __init__(self):
        super().__init__()
        self.net = nn.Sequential(nn.Linear(G + K, 64), nn.ReLU(), nn.Linear(64, 64), nn.ReLU(), nn.Linear(64, 1))

    def forward(self, x):
        return self.net(x).squeeze(-1)


def export(model, path):
    lin = [m for m in model.net if isinstance(m, nn.Linear)]
    fmt = lambda v: ' '.join(repr(float(x)) for x in v)
    lines = ['PolicyModel 1', 'layers ' + ' '.join(str(s) for s in [IN, *HIDDEN, 1]),
             'mean ' + fmt(model.mean.tolist()), 'std ' + fmt(model.std.tolist())]
    for l in lin:
        lines.append('w ' + fmt(l.weight.detach().reshape(-1).tolist()))
        lines.append('b ' + fmt(l.bias.detach().tolist()))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'w', newline='\n').write('\n'.join(lines) + '\n')


def load(path):
    lines = open(path).read().split('\n')
    floats = lambda s: [float(x) for x in s.split()[1:]]
    m = Scorer(floats(lines[2]), floats(lines[3]))
    lin = [x for x in m.net if isinstance(x, nn.Linear)]
    for i, l in enumerate(lin):
        l.weight.data = torch.tensor(floats(lines[4 + 2 * i]), dtype=torch.float32).reshape(l.weight.shape)
        l.bias.data = torch.tensor(floats(lines[5 + 2 * i]), dtype=torch.float32)
    return m


# ---------------- 배치 ----------------

def flatten(games, keep_team=None):
    """결정 → 패딩한 텐서. keep_team(game) → 남길 팀 번호 집합."""
    rows = []
    for gi, gm in enumerate(games):
        teams = keep_team(gm) if keep_team else None
        for d in gm['dec']:
            if teams is not None and d[0] not in teams:
                continue
            rows.append((gi, *d))
    nmax = max(len(r[7]) for r in rows)
    N = len(rows)
    x = np.zeros((N, nmax, IN), np.float32)
    mask = np.zeros((N, nmax), bool)
    chosen = np.zeros(N, np.int64)
    logp = np.zeros(N, np.float32)
    gk = np.zeros((N, G + K), np.float32)
    meta = []
    for i, (gi, team, turn, kind, ch, lp, glob_, cands) in enumerate(rows):
        n = len(cands)
        x[i, :n, :G] = glob_
        x[i, :n, G + kind] = 1
        x[i, :n, G + K:] = cands
        mask[i, :n] = True
        chosen[i] = ch
        logp[i] = lp
        gk[i, :G] = glob_
        gk[i, G + kind] = 1
        meta.append((gi, team, turn))
    return dict(x=torch.from_numpy(x), mask=torch.from_numpy(mask), chosen=torch.from_numpy(chosen),
                logp=torch.from_numpy(logp), gk=torch.from_numpy(gk), meta=meta)


def logits(model, x, mask):
    s = model(x)
    return s.masked_fill(~mask, -1e9)


# ---------------- 명령 ----------------

def cmd_bc(a):
    print(f'collecting {a.games} rule-vs-rule games on {WORKERS} workers…', flush=True)
    per = max(1, a.games // (WORKERS * 4))
    jobs = [(per, 100000 + i * per, 'rule', 'rule', 'both', 1.0) for i in range(a.games // per)]
    t = time.time()
    games = run_rollouts(jobs)
    print(f'  {len(games)} games in {time.time() - t:.0f}s', flush=True)
    random.Random(0).shuffle(games)
    split = max(1, len(games) // 10)
    tr, va = flatten(games[split:]), flatten(games[:split])
    m = tr['mask']
    flat = tr['x'][m]
    mean, std = flat.mean(0), flat.std(0).clamp_min(1e-3)
    mean[G:G + K] = 0; std[G:G + K] = 1  # 원핫은 정규화하지 않는다
    model = Scorer(mean.tolist(), std.tolist())
    opt = torch.optim.Adam(model.parameters(), lr=1e-3)
    N = len(tr['chosen'])
    for ep in range(a.epochs):
        perm = torch.randperm(N)
        model.train()
        for i in range(0, N, 2048):
            idx = perm[i:i + 2048]
            loss = nn.functional.cross_entropy(logits(model, tr['x'][idx], tr['mask'][idx]), tr['chosen'][idx])
            opt.zero_grad(); loss.backward(); opt.step()
        with torch.no_grad():
            lv = logits(model, va['x'], va['mask'])
            acc = (lv.argmax(1) == va['chosen']).float().mean().item()
            vl = nn.functional.cross_entropy(lv, va['chosen']).item()
        print(f'  epoch {ep}: val loss {vl:.4f} acc {acc:.4f}', flush=True)
    export(model, a.out)
    print('saved', a.out)


def outcome(gm, team):
    """그 팀의 결과 보상: 승 +1, 패 -1, 턴 제한은 점수 차(±0.5 이내)."""
    if gm['winner'] >= 0:
        return 1.0 if gm['winner'] == team else -1.0
    sp, se = gm['score']
    own, opp = (sp, se) if team == 0 else (se, sp)
    return float(np.clip((own - opp) / 2000.0, -0.5, 0.5))


def cmd_ppo(a):
    os.makedirs(RUNS, exist_ok=True)
    state_path = os.path.join(RUNS, 'ppo_state.json')
    state = json.load(open(state_path)) if os.path.exists(state_path) and a.resume else {'iter': 0, 'snapshots': [], 'log': []}
    model = load(state['snapshots'][-1] if state['snapshots'] else a.init)
    value = Value()
    vpath = os.path.join(RUNS, 'value.pt')
    if a.resume and os.path.exists(vpath):
        value.load_state_dict(torch.load(vpath))
    opt = torch.optim.Adam(model.parameters(), lr=a.lr)
    vopt = torch.optim.Adam(value.parameters(), lr=1e-3)
    current = os.path.join(RUNS, 'current.txt')
    if not state['snapshots']:
        # 모방 정책은 규칙 AI의 선택을 거의 확신한다(탐색이 안 된다) — 출력층을 줄여 처음부터 조금 다른 수도 둬 보게 한다.
        last = [m for m in model.net if isinstance(m, nn.Linear)][-1]
        last.weight.data *= a.init_scale; last.bias.data *= a.init_scale
        snap = os.path.join(RUNS, 'ppo_0000.txt'); export(model, snap); state['snapshots'].append(snap)

    for it in range(state['iter'], state['iter'] + a.iters):
        t0 = time.time()
        export(model, current)
        shaping = max(0.0, 1.0 - it / max(1, a.shaping_iters))
        jobs = []
        for w in range(WORKERS * a.jobs_per_worker):
            r = random.random()
            opp = 'rule' if r < a.p_rule else (random.choice(state['snapshots']) if r < a.p_rule + a.p_past else current)
            seed = 1_000_000 + it * 10_000 + w * a.games_per_job
            if w % 2 == 0:
                jobs.append((a.games_per_job, seed, current, opp, 'player', 1.0))
            else:
                jobs.append((a.games_per_job, seed, opp, current, 'enemy', 1.0))
        games = run_rollouts(jobs)
        t1 = time.time()
        rec_team = lambda gm: {0} if gm['record'] == 'player' else {1}
        b = flatten(games, rec_team)
        # 목표값: 최종 결과 + (감쇠하는) 점수 차 잠재 보상 Φ_T - Φ_t
        target = np.zeros(len(b['meta']), np.float32)
        for i, (gi, team, turn) in enumerate(b['meta']):
            gm = games[gi]
            sp, se = gm['score']
            own, opp_ = (sp, se) if team == 0 else (se, sp)
            phi_t = b['gk'][i, 9].item() - b['gk'][i, 10].item()
            phi_T = (own - opp_) / 2000.0
            target[i] = outcome(gm, team) + shaping * (phi_T - phi_t)
        target = torch.from_numpy(target)
        with torch.no_grad():
            adv = target - value(b['gk'])
            adv = (adv - adv.mean()) / (adv.std() + 1e-6)
        N = len(target)
        for ep in range(a.epochs):
            perm = torch.randperm(N)
            for i in range(0, N, a.batch):
                idx = perm[i:i + a.batch]
                lg = logits(model, b['x'][idx], b['mask'][idx])
                lsm = torch.log_softmax(lg, 1)
                lp = lsm.gather(1, b['chosen'][idx][:, None]).squeeze(1)
                ratio = torch.exp(lp - b['logp'][idx])
                pg = -torch.min(ratio * adv[idx], ratio.clamp(1 - a.clip, 1 + a.clip) * adv[idx]).mean()
                ent = -(lsm.exp() * lsm.masked_fill(~b['mask'][idx], 0)).sum(1).mean()
                loss = pg - a.entropy * ent
                opt.zero_grad(); loss.backward(); nn.utils.clip_grad_norm_(model.parameters(), 1.0); opt.step()
                vl = ((value(b['gk'][idx]) - target[idx]) ** 2).mean()
                vopt.zero_grad(); vl.backward(); vopt.step()
        wins = sum(1 for gm in games if outcome(gm, 0 if gm['record'] == 'player' else 1) > 0)
        msg = dict(iter=it, games=len(games), decisions=N, winrate=wins / len(games), shaping=round(shaping, 3),
                   roll_s=round(t1 - t0), upd_s=round(time.time() - t1))
        if (it + 1) % a.snapshot_every == 0:
            snap = os.path.join(RUNS, f'ppo_{it + 1:04d}.txt')
            export(model, snap)
            state['snapshots'].append(snap)
            w, l, d = evaluate(snap, 'rule', a.eval_games)
            msg['vs_rule'] = round((w + 0.5 * d) / max(1, w + l + d), 3)
        state['iter'] = it + 1
        state['log'].append(msg)
        json.dump(state, open(state_path, 'w'), indent=1)
        torch.save(value.state_dict(), vpath)
        print(json.dumps(msg), flush=True)


def cmd_eval(a):
    w, l, d = evaluate(a.a, a.b, a.games, ta=a.ta, ba=a.ba, tb=a.tb, bb=a.bb)
    print(f'{a.a} vs {a.b}: W{w} L{l} D{d}  score={(w + 0.5 * d) / max(1, w + l + d):.3f}')


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest='cmd', required=True)
    b = sub.add_parser('bc'); b.add_argument('--games', type=int, default=1000); b.add_argument('--epochs', type=int, default=8)
    b.add_argument('--out', default=os.path.join(RUNS, 'bc.txt'))
    q = sub.add_parser('ppo'); q.add_argument('--init', default=os.path.join(RUNS, 'bc.txt')); q.add_argument('--iters', type=int, default=100)
    q.add_argument('--resume', action='store_true'); q.add_argument('--lr', type=float, default=3e-4)
    q.add_argument('--games-per-job', type=int, default=4); q.add_argument('--jobs-per-worker', type=int, default=2)
    q.add_argument('--p-rule', type=float, default=0.3); q.add_argument('--p-past', type=float, default=0.3)
    q.add_argument('--epochs', type=int, default=3); q.add_argument('--batch', type=int, default=4096)
    q.add_argument('--clip', type=float, default=0.2); q.add_argument('--entropy', type=float, default=0.01)
    q.add_argument('--shaping-iters', type=int, default=60); q.add_argument('--snapshot-every', type=int, default=10)
    q.add_argument('--eval-games', type=int, default=112); q.add_argument('--init-scale', type=float, default=0.25)
    e = sub.add_parser('eval'); e.add_argument('--a', required=True); e.add_argument('--b', required=True)
    e.add_argument('--games', type=int, default=112)
    for n in ('ta', 'ba', 'tb', 'bb'):
        e.add_argument('--' + n, type=float, default=0.0)
    a = p.parse_args()
    {'bc': cmd_bc, 'ppo': cmd_ppo, 'eval': cmd_eval}[a.cmd](a)


if __name__ == '__main__':
    main()
