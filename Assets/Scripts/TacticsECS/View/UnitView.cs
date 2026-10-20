using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace TacticsECS
{
    /// <summary>2D presentation of an entity; simulation, actions and stats remain in Systems/Data.</summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }
        public string Label { get; private set; }
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private Transform hpDisplayPrefab;
        [SerializeField] private Transform damagePopupPrefab;
        public static readonly Vector2 HpBarSize = new Vector2(.6f,.0625f);
        public const float HpBarLocalY = .46875f; // just above the tallest unit art (tile-filling sprites)
        public const float HpNumberLocalY = .59375f;
        private UnitDefinition _definition;
        private GridWorld _grid;
        private GameObject _model;
        private Transform _hpGroup, _hpBarFill;
        private TextMesh _hpText;
        private SpriteRenderer _hpFill, _teamBadge, _guardBadge, _statusBadge, _poisonBadge;
        private GameObject _alert; // 근처에 숨은 적이 있을 때 머리 위 (!) — docs/spec/game/vision.md#은신
        private SortingGroup _sorting;
        private Coroutine _moveRoutine;
        private Coroutine _attackRoutine, _hitRoutine, _deathRoutine;
        private Vector3 _moveTarget;
        private List<Vector3> _movePath; // 다음 이동이 따라갈 칸 중심들(SetMovePath)
        // 아직 피해 숫자가 안 뜬 피격 수(AwaitHit). 그동안 체력 표시는 맞기 전 값에 머물고, 죽었어도 사라지지 않는다.
        private int _pendingHits;
        private float _awaitUntil;
        private int _shownHp = -1, _actualHp, _maxHp;
        private float _lastHitTime = -1;
        private Color _bodyTint = Color.white;
        private bool _visible = true;
        private Vector3 _bodyOrigin;
        // Multiply the unit sheet's grey team ramp into the reference pack's blue/red (scripts/author_pixel_patterns.py TEAM_GAIN).
        public static readonly Color PlayerColor = new Color(.29f,.54f,1f);
        public static readonly Color EnemyColor = new Color(.97f,0f,.31f);
        private static readonly Color MythicTint = new Color(1f,.86f,.45f);
        private static readonly Color SpentTint = new Color(.7f,.7f,.75f); // 이번 턴 더 할 일이 없는 아군
        private static readonly Color HealColor = new Color(.35f,.9f,.4f);
        public void Init(EntityWorld world, int id, GridWorld grid, string label)
        {
            UnitId = id; Label = label; _grid = grid; _definition = GetComponent<UnitDefinition>();
            // Legacy models are disabled defensively; the migration CLI removes them from source prefabs.
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.gameObject.SetActive(false);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(grid.TileSize,grid.TileSize,1);
            _sorting = GetComponent<SortingGroup>();
            if (_sorting == null) _sorting = gameObject.AddComponent<SortingGroup>();
            _model = PixelSpriteCatalog.Build(_definition.ModelId,transform,order:1);
            PixelSpriteCatalog.Add(transform,"Shadow",PixelSpriteCatalog.Get("UI.Shadow"),new Vector2(0,-.34375f),new Vector2(.5f,.125f),new Color(.1f,.14f,.2f,.3f),-1);
            _teamBadge = PixelSpriteCatalog.Rectangle(transform,"Team",new Vector2(0,-.4375f),new Vector2(.5f,.0625f),Color.white,3);
            _guardBadge = PixelSpriteCatalog.Add(transform,"Guarding",PixelSpriteComposer.Compose("Icon.guard"),new Vector2(.3125f,.1875f),Vector2.one,Color.white,4);
            _statusBadge = PixelSpriteCatalog.Add(transform,"Status",PixelSpriteComposer.Compose("Icon.freeze"),new Vector2(-.3125f,.1875f),Vector2.one,Color.white,4);
            _poisonBadge = PixelSpriteCatalog.Rectangle(transform,"Poison",new Vector2(-.3125f,-.125f),new Vector2(.1875f,.1875f),new Color(.55f,.85f,.2f),4);
            _alert = new GameObject("HiddenAlert"); _alert.transform.SetParent(transform,false);
            var alertColor = new Color(1f,.85f,.2f);
            // 체력 숫자(HpNumberLocalY) 오른쪽에 둬서 숫자와 겹치지 않게 한다.
            PixelSpriteCatalog.Rectangle(_alert.transform,"Bar",new Vector2(.40625f,.65625f),new Vector2(.09375f,.25f),alertColor,23);
            PixelSpriteCatalog.Rectangle(_alert.transform,"Dot",new Vector2(.40625f,.46875f),new Vector2(.09375f,.09375f),alertColor,23);
            BuildHpDisplay();
            transform.position = PixelCoordinates.GridToWorld(grid,world.Get<GridPosition>(id).Value);
            Refresh(world,id);
        }
        private void BuildHpDisplay()
        {
            if (hpDisplayPrefab == null) throw new System.InvalidOperationException("UnitView needs HpDisplay prefab");
            _hpGroup = Instantiate(hpDisplayPrefab,transform); _hpGroup.name = "HpDisplay";
            _hpGroup.localRotation = Quaternion.identity;
            _hpBarFill = _hpGroup.Find("Bar_Fill"); _hpFill = _hpBarFill.GetComponent<SpriteRenderer>();
            _hpText = _hpGroup.GetComponentInChildren<TextMesh>();
            _hpGroup.Find("Bar_Bg").GetComponent<Renderer>().sortingOrder = 20;
            _hpBarFill.GetComponent<Renderer>().sortingOrder = 21;
            _hpText.GetComponent<Renderer>().sortingOrder = 22;
        }
        public void Refresh(EntityWorld world, int id)
        {
            if (!UnitQueries.IsAlive(world,id))
            {
                _actualHp = 0;
                // 죽은 유닛은 맞는 모습을 보인 뒤 깜빡이며 사라진다(Play 모드).
                if (Application.isPlaying && gameObject.activeInHierarchy) { if (_deathRoutine == null) _deathRoutine = StartCoroutine(DeathRoutine()); }
                else gameObject.SetActive(false);
                return;
            }
            if (_deathRoutine != null) { StopCoroutine(_deathRoutine); _deathRoutine = null; SetAlpha(1); }
            if (!gameObject.activeSelf) gameObject.SetActive(true); // 강림에서 돌아온 원본
            int hp = world.Get<Hp>(id).Value;
            int maxHp = world.GetOrDefault<MaxHp>(id).Value;
            if (maxHp <= 0) maxHp = _definition.MaxHp;
            _actualHp = hp; _maxHp = maxHp;
            if (_pendingHits > 0 && Time.time > _awaitUntil) _pendingHits = 0; // 피해 숫자가 끝내 안 뜬 경우
            ShowHp(_pendingHits > 0 && _shownHp > hp ? _shownHp : hp);
            _hpText.color = world.GetOrDefault<Veteran>(id).Value ? new Color(1f,.82f,.25f) : Color.white;
            var teamColor = world.Get<Team>(id) == Team.Player ? PlayerColor : EnemyColor;
            _teamBadge.color = teamColor;
            _teamBadge.transform.localScale = world.Get<Team>(id) == Team.Player ? new Vector3(.5f,.0625f,1) : new Vector3(.375f,.125f,1);
            _guardBadge.gameObject.SetActive(world.Get<IsGuarding>(id).Value);
            bool frozen = world.GetOrDefault<Frozen>(id).Value;
            bool hidden = world.GetOrDefault<Hidden>(id).Value;
            bool praying = world.GetOrDefault<Praying>(id).Value;
            _statusBadge.gameObject.SetActive(frozen || praying || hidden);
            _statusBadge.sprite = PixelSpriteComposer.Compose(frozen ? "Icon.freeze" : praying ? "Icon.heal" : "Icon.infiltrate");
            _poisonBadge.gameObject.SetActive(world.GetOrDefault<Poisoned>(id).Value);
            // 플레이어 유닛 옆(8칸)에 숨은 적이 있으면 위치는 모른 채 (!)만 보인다.
            _alert.SetActive(world.Get<Team>(id) == Team.Player && StealthSystem.AdjacentHiddenEnemy(_grid,world,id));
            var embarked = world.GetOrDefault<Embarked>(id);
            _bodyOrigin = Vector3.zero;
            _model.transform.localPosition = _bodyOrigin;
            string visual = PixelSpriteCatalog.Has(_definition.ModelId) ? _definition.ModelId : "UI.Missing";
            // Embarked units show the ship alone (the ship sprites fill the tile); team pixels take the team colour.
            var body = PixelSpriteComposer.Compose(embarked.Value ? "Boat."+embarked.NavalUnitId : visual,team:teamColor);
            var bodyRenderer = _model.GetComponentInChildren<SpriteRenderer>();
            bodyRenderer.sprite = body;
            var tint = world.GetOrDefault<Mythic>(id).Active ? MythicTint : Color.white; // 신화 유닛은 금빛
            if (IsSpent(world,id)) tint *= SpentTint;
            _bodyTint = tint;
            if (_hitRoutine == null) bodyRenderer.color = tint;
            _model.transform.localScale = new Vector3(_model.transform.localScale.x < 0 ? -1 : 1,1,1);
            var target = PixelCoordinates.GridToWorld(_grid,world.Get<GridPosition>(id).Value);
            bool moving = _moveRoutine != null && (_moveTarget-target).sqrMagnitude < .0001f;
            if (!moving && (target-transform.position).sqrMagnitude > .0001f)
            {
                if (_moveRoutine != null) StopCoroutine(_moveRoutine);
                if (Application.isPlaying) { _moveTarget = target; _moveRoutine = StartCoroutine(MoveRoutine(TakePath(target))); }
                else transform.position = target;
            }
            _movePath = null;
            UpdateSort();
            ApplyVisibility();
        }
        private void ShowHp(int hp)
        {
            _shownHp = hp;
            _hpText.text = hp.ToString();
            float fraction = _maxHp > 0 ? Mathf.Clamp01((float)hp/_maxHp) : 0;
            float width = HpBarSize.x*fraction;
            _hpBarFill.localScale = new Vector3(width,HpBarSize.y,1);
            _hpBarFill.localPosition = new Vector3((width-HpBarSize.x)/2,HpBarLocalY,0);
            if (_hpFill != null) _hpFill.color = HpColorScale.ForFraction(fraction);
        }
        public void SetVisible(bool visible) { _visible = visible; ApplyVisibility(); }
        private void ApplyVisibility() { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = _visible; }
        public void FaceTowards(Vector3 worldPosition)
        {
            float dx = worldPosition.x-transform.position.x;
            if (Mathf.Abs(dx) < .001f) return;
            var scale = _model.transform.localScale; scale.x = Mathf.Abs(scale.x)*(dx < 0 ? -1 : 1); _model.transform.localScale = scale;
        }
        public void PlayAttackTowards(Vector3 worldPosition)
        {
            FaceTowards(worldPosition);
            if (!Application.isPlaying || !gameObject.activeInHierarchy) return;
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AttackPulse(worldPosition - transform.position));
        }
        private IEnumerator AttackPulse(Vector3 direction)
        {
            var origin = _bodyOrigin;
            var offset = direction.normalized * .125f;
            float elapsed = 0;
            while (elapsed < .18f)
            {
                elapsed += Time.deltaTime;
                var position = origin + offset * Mathf.Sin(Mathf.Clamp01(elapsed / .18f) * Mathf.PI);
                position.x = Mathf.Round(position.x * 16) / 16;
                position.y = Mathf.Round(position.y * 16) / 16;
                _model.transform.localPosition = position;
                yield return null;
            }
            _model.transform.localPosition = _bodyOrigin;
            _attackRoutine = null;
        }
        /// <summary>내 턴에 이 아군이 더 할 수 있는 행동이 없는지 — 행동을 마쳤고 더 움직일 수도 없다(빙결 포함).</summary>
        private static bool IsSpent(EntityWorld world, int id)
        {
            if (world.Get<Team>(id) != Team.Player || !world.Get<HasActed>(id).Value) return false;
            var move = UnitActionQueries.Find<MoveAction>(world,id);
            return move == null || !move.CanExecute(world,id);
        }
        /// <summary>이동 때 한 칸을 걷는 데 걸리는 시간.</summary>
        public float StepSeconds => _grid.TileSize / moveSpeed;
        /// <summary>다음 Refresh의 이동이 이 칸들(출발 칸 제외, 도착 칸 포함)을 차례로 지나가게 한다.</summary>
        public void SetMovePath(List<Vector2Int> path)
        {
            _movePath = new List<Vector3>();
            foreach (var p in path) _movePath.Add(PixelCoordinates.GridToWorld(_grid,p));
        }
        private List<Vector3> TakePath(Vector3 target)
        {
            var path = _movePath; _movePath = null;
            if (path != null)
            {
                int end = path.FindIndex(p => (p-target).sqrMagnitude < .0001f); // 숨은 적에게 막히면 경로 중간에서 멈춘다
                if (end >= 0) return path.GetRange(0,end+1);
            }
            return new List<Vector3> { target };
        }
        /// <summary>곧 피해 팝업이 뜰 유닛: 그 전에 죽었다고 Refresh되어도 팝업이 뜰 때까지 남아 있는다.</summary>
        /// <summary>곧 피해 숫자가 뜰 피격 하나를 예고한다 — 그 숫자가 뜰 때(ShowDamagePopup)까지 체력 표시를 내리지 않는다.</summary>
        public void AwaitHit() { _pendingHits++; _awaitUntil = Time.time+3f; }
        private IEnumerator DeathRoutine()
        {
            float start = Time.time;
            while ((_pendingHits > 0 && Time.time < start+3f) || Time.time < start+.2f || Time.time < _lastHitTime+.3f) yield return null;
            for (int i=0;i<6;i++) { SetAlpha(i%2==0 ? .25f : 1f); yield return new WaitForSeconds(.06f); }
            SetAlpha(1); _deathRoutine = null; _pendingHits = 0;
            gameObject.SetActive(false);
        }
        private void SetAlpha(float alpha)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) { var c = r.color; c.a = alpha; r.color = c; }
            if (_hpText != null) { var c = _hpText.color; c.a = alpha; _hpText.color = c; }
        }
        private void UpdateSort() => _sorting.sortingOrder = PixelSpriteCatalog.SortOrder(transform.position.y,true);
        /// <summary>칸마다 한 번 통통 뛰며 경로를 따라간다.</summary>
        private IEnumerator MoveRoutine(List<Vector3> path)
        {
            float ppu = PixelSpriteCatalog.PixelsPerUnit / _grid.TileSize;
            foreach (var worldPos in path)
            {
                FaceTowards(worldPos);
                var from = transform.position;
                float length = Mathf.Max((worldPos-from).magnitude,.0001f), travelled = 0;
                while (travelled < length)
                {
                    travelled = Mathf.Min(length,travelled+moveSpeed*Time.deltaTime);
                    var smooth = Vector3.Lerp(from,worldPos,travelled/length);
                    transform.position = new Vector3(Mathf.Round(smooth.x * ppu) / ppu, Mathf.Round(smooth.y * ppu) / ppu, 0);
                    float hop = Mathf.Sin(travelled/length*Mathf.PI) * .09375f;
                    _model.transform.localPosition = _bodyOrigin + new Vector3(0,Mathf.Round(hop*16)/16,0);
                    UpdateSort(); yield return null;
                }
                transform.position = worldPos;
            }
            _model.transform.localPosition = _bodyOrigin; UpdateSort(); _moveRoutine = null;
        }
        public void ShowDamagePopup(int amount)
        {
            if (damagePopupPrefab == null) return;
            var instance = Instantiate(damagePopupPrefab,transform.position+Vector3.up*HpNumberLocalY*_grid.TileSize,Quaternion.identity);
            instance.GetComponent<Renderer>().sortingOrder = PixelSpriteCatalog.OverlayOrder+10;
            instance.GetComponent<DamagePopup>().Play("-"+amount,new Color(.95f,.25f,.2f));
            if (_pendingHits > 0) _pendingHits--;
            // 맞는 순간 체력 표시를 내린다. 남은 피격이 있으면 이번 피해만큼만(실제 값 아래로는 안 내려간다).
            if (_shownHp >= 0) ShowHp(_pendingHits > 0 ? Mathf.Max(_actualHp, _shownHp-amount) : _actualHp);
            _lastHitTime = Time.time;
            if (Application.isPlaying && gameObject.activeInHierarchy && _hitRoutine == null)
                _hitRoutine = StartCoroutine(HitPulse());
        }
        private IEnumerator HitPulse()
        {
            var renderers = _model.GetComponentsInChildren<SpriteRenderer>();
            var colors = new Color[renderers.Length];
            for (int i=0;i<renderers.Length;i++) { colors[i]=renderers[i].color; renderers[i].color=new Color(1,.45f,.4f); }
            yield return new WaitForSeconds(.12f);
            for (int i=0;i<renderers.Length;i++) if (renderers[i] != null) renderers[i].color=colors[i];
            _hitRoutine = null;
            var body = _model.GetComponentInChildren<SpriteRenderer>();
            if (body != null) body.color = new Color(_bodyTint.r,_bodyTint.g,_bodyTint.b,body.color.a); // 맞는 동안 바뀐 색(행동 마침 등)
        }
        public void ShowHealPopup(int amount)
        {
            if (damagePopupPrefab == null || amount <= 0) return;
            var instance = Instantiate(damagePopupPrefab,transform.position+Vector3.up*HpNumberLocalY*_grid.TileSize,Quaternion.identity);
            instance.GetComponent<Renderer>().sortingOrder = PixelSpriteCatalog.OverlayOrder+10;
            instance.GetComponent<DamagePopup>().Play("+"+amount,HealColor);
        }
    }
}
