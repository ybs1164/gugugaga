using System.Collections;
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
        private Coroutine _attackRoutine, _hitRoutine;
        private bool _visible = true;
        private Vector3 _bodyOrigin;
        // Multiply the unit sheet's grey team ramp into the reference pack's blue/red (scripts/author_pixel_patterns.py TEAM_GAIN).
        public static readonly Color PlayerColor = new Color(.29f,.54f,1f);
        public static readonly Color EnemyColor = new Color(.97f,0f,.31f);
        private static readonly Color MythicTint = new Color(1f,.86f,.45f);
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
            _poisonBadge = PixelSpriteCatalog.Rectangle(transform,"Poison",new Vector2(-.3125f,-.125f),new Vector2(.125f,.125f),new Color(.55f,.85f,.2f),4);
            _alert = new GameObject("HiddenAlert"); _alert.transform.SetParent(transform,false);
            var alertColor = new Color(1f,.85f,.2f);
            PixelSpriteCatalog.Rectangle(_alert.transform,"Bar",new Vector2(0,.8125f),new Vector2(.0625f,.1875f),alertColor,23);
            PixelSpriteCatalog.Rectangle(_alert.transform,"Dot",new Vector2(0,.6875f),new Vector2(.0625f,.0625f),alertColor,23);
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
            if (!UnitQueries.IsAlive(world,id)) { gameObject.SetActive(false); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true); // 강림에서 돌아온 원본
            int hp = world.Get<Hp>(id).Value;
            int maxHp = world.GetOrDefault<MaxHp>(id).Value;
            if (maxHp <= 0) maxHp = _definition.MaxHp;
            _hpText.text = hp.ToString();
            _hpText.color = world.GetOrDefault<Veteran>(id).Value ? new Color(1f,.82f,.25f) : Color.white;
            float fraction = maxHp > 0 ? Mathf.Clamp01((float)hp/maxHp) : 0;
            float width = HpBarSize.x*fraction;
            _hpBarFill.localScale = new Vector3(width,HpBarSize.y,1);
            _hpBarFill.localPosition = new Vector3((width-HpBarSize.x)/2,HpBarLocalY,0);
            if (_hpFill != null) _hpFill.color = HpColorScale.ForFraction(fraction);
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
            bodyRenderer.color = world.GetOrDefault<Mythic>(id).Active ? MythicTint : Color.white; // 신화 유닛은 금빛
            _model.transform.localScale = new Vector3(_model.transform.localScale.x < 0 ? -1 : 1,1,1);
            var target = PixelCoordinates.GridToWorld(_grid,world.Get<GridPosition>(id).Value);
            if ((target-transform.position).sqrMagnitude > .0001f)
            {
                if (_moveRoutine != null) StopCoroutine(_moveRoutine);
                if (Application.isPlaying) _moveRoutine = StartCoroutine(MoveRoutine(target));
                else transform.position = target;
            }
            UpdateSort();
            ApplyVisibility();
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
        private void UpdateSort() => _sorting.sortingOrder = PixelSpriteCatalog.SortOrder(transform.position.y,true);
        private IEnumerator MoveRoutine(Vector3 worldPos)
        {
            FaceTowards(worldPos);
            var smoothPosition = transform.position;
            float ppu = PixelSpriteCatalog.PixelsPerUnit / _grid.TileSize;
            while ((smoothPosition-worldPos).sqrMagnitude > .0001f)
            {
                smoothPosition = Vector3.MoveTowards(smoothPosition,worldPos,moveSpeed*Time.deltaTime);
                transform.position = new Vector3(Mathf.Round(smoothPosition.x * ppu) / ppu, Mathf.Round(smoothPosition.y * ppu) / ppu, 0);
                UpdateSort(); yield return null;
            }
            transform.position = worldPos; UpdateSort(); _moveRoutine = null;
        }
        public void ShowDamagePopup(int amount)
        {
            if (damagePopupPrefab == null) return;
            var instance = Instantiate(damagePopupPrefab,transform.position+Vector3.up*HpNumberLocalY*_grid.TileSize,Quaternion.identity);
            instance.GetComponent<Renderer>().sortingOrder = PixelSpriteCatalog.OverlayOrder+10;
            instance.GetComponent<DamagePopup>().Play("-"+amount,new Color(.95f,.25f,.2f));
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
        }
    }
}
