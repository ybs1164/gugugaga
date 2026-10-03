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
        public const float HpBarLocalY = .3125f;
        public const float HpNumberLocalY = .4375f;
        private const int EmbarkedRiderX = 3, EmbarkedRiderY = 7;
        private UnitDefinition _definition;
        private GridWorld _grid;
        private GameObject _model;
        private Transform _hpGroup, _hpBarFill;
        private TextMesh _hpText;
        private SpriteRenderer _hpFill, _teamBadge, _guardBadge, _statusBadge;
        private SortingGroup _sorting;
        private Coroutine _moveRoutine;
        private Coroutine _attackRoutine, _hitRoutine;
        private bool _visible = true;
        private Vector3 _bodyOrigin;
        private static readonly Color PlayerColor = new Color(.3f,.6f,1f);
        private static readonly Color EnemyColor = new Color(1f,.36f,.29f);
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
            _guardBadge = PixelSpriteCatalog.Add(transform,"Guarding",PixelSpriteComposer.Compose("Icon.guard",half:true),new Vector2(.3125f,.1875f),Vector2.one,Color.white,4);
            _statusBadge = PixelSpriteCatalog.Add(transform,"Status",PixelSpriteComposer.Compose("Icon.freeze",half:true),new Vector2(-.3125f,.1875f),Vector2.one,Color.white,4);
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
            _teamBadge.color = world.Get<Team>(id) == Team.Player ? PlayerColor : EnemyColor;
            _teamBadge.transform.localScale = world.Get<Team>(id) == Team.Player ? new Vector3(.5f,.0625f,1) : new Vector3(.375f,.125f,1);
            _guardBadge.gameObject.SetActive(world.Get<IsGuarding>(id).Value);
            bool frozen = world.GetOrDefault<Frozen>(id).Value;
            bool hidden = world.GetOrDefault<Hidden>(id).Value;
            _statusBadge.gameObject.SetActive(frozen || hidden);
            _statusBadge.sprite = PixelSpriteComposer.Compose(frozen ? "Icon.freeze" : "Icon.infiltrate",half:true);
            var embarked = world.GetOrDefault<Embarked>(id);
            _bodyOrigin = Vector3.zero;
            _model.transform.localPosition = _bodyOrigin;
            string visual = PixelSpriteCatalog.Has(_definition.ModelId) ? _definition.ModelId : "UI.Missing";
            // Embarked: hull first, rider standing in it, baked into one sprite with one outline style.
            var body = embarked.Value
                ? PixelSpriteComposer.Compose(new[] { new SpritePlacement("Boat."+embarked.NavalUnitId), new SpritePlacement(visual,EmbarkedRiderX,EmbarkedRiderY) })
                : PixelSpriteComposer.Compose(visual);
            _model.GetComponentInChildren<SpriteRenderer>().sprite = body;
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
