"""난이도 사다리: 후보(모델 × 온도 × 실수 확률)마다 규칙 AI 상대 점수를 잰다. 다섯을 고르는 것과 이웃 단계 대국(train.py eval)은 사람이 한다.

사용: python tools/ai/ladder.py [--games 224] [--models tools/ai/runs/ppo_0040.txt ...]
결과: tools/ai/runs/ladder.json (후보별 점수). 점수 = (승 + 무/2) / 판.
고른 다섯은 손으로 확인한 뒤 Assets/Resources/AI/에 모델을 복사하고 AIDifficulty.csv에 옮긴다(README "AI 난이도 학습").
"""
import argparse
import glob
import json
import os

import train

RUNS = train.RUNS


def candidates(models):
    out = [dict(name='rule', model='rule', t=0.0, b=0.0)]
    weak = models[0]
    for t, b in [(3.0, 0.5), (2.0, 0.4), (1.0, 0.3), (1.0, 0.15)]:
        out.append(dict(name=f'{os.path.basename(weak)} t{t} b{b}', model=weak, t=t, b=b))
    for m in models:
        out.append(dict(name=f'{os.path.basename(m)} greedy', model=m, t=0.0, b=0.0))
    return out


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--games', type=int, default=224)
    p.add_argument('--models', nargs='*')
    a = p.parse_args()
    models = a.models or sorted(glob.glob(os.path.join(RUNS, 'ppo_*.txt')))
    rows = []
    for c in candidates(models):
        if c['model'] == 'rule':
            c['vs_rule'] = 0.5
        else:
            w, l, d = train.evaluate(c['model'], 'rule', a.games, ta=c['t'], ba=c['b'])
            c['vs_rule'] = round((w + 0.5 * d) / max(1, w + l + d), 3)
        rows.append(c)
        print(f"{c['name']:<40} vs rule {c['vs_rule']:.3f}", flush=True)
    json.dump(dict(candidates=rows), open(os.path.join(RUNS, 'ladder.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
