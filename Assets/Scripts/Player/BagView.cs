using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.Player
{
    /// <summary>
    /// The bag is not a menu: hold Tab and the hero leans down and lays a cloth on the ground below him, inside the
    /// diorama, with everything in the bag on top of it. Time slows to a crawl but does not stop, it makes a little
    /// noise, and being hit packs it up at once. Arrows pick a space, Z uses the item, A S D F put it in a belt pocket
    /// (on an empty space they take that pocket back into the bag). Letting go of Tab packs everything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BagView : MonoBehaviour
    {
        [Header("Links")]
        public Inventory inventory;
        public HeroCombat combat;
        public DioramaHero hero;
        public Health health;
        [Tooltip("The UI canvas that receives the label under the cloth.")]
        public Transform canvasRoot;

        [Header("Look (existing diorama materials)")]
        public Material clothMaterial;
        public Material cellMaterial;
        public Material cursorMaterial;

        [Header("Layout")]
        [SerializeField] int columns = 6;
        [SerializeField] float cellSize = 0.8f;
        [Tooltip("Same yaw as the camera, so the cloth reads upright on screen.")]
        [SerializeField] float clothYaw = 45f;
        [SerializeField] float leanDegrees = 30f;

        [Header("Feel")]
        [Range(0.02f, 1f)] [SerializeField] float slowScale = 0.12f;
        [SerializeField] float noiseRadius = 2.5f;
        [SerializeField] float noiseEvery = 0.8f;

        public bool IsOpen { get { return _open; } }

        bool _open, _needRelease;
        int _cursor;
        float _noiseTimer;
        Quaternion _bodyAtOpen = Quaternion.identity;
        Transform _root, _cursorT;
        readonly List<GameObject> _items = new List<GameObject>();
        GameObject _panel;
        Text _title, _detail;

        int Rows { get { return Mathf.CeilToInt(inventory.BagCapacity / (float)columns); } }

        void Update()
        {
            if (inventory == null || combat == null || health == null) return;
            bool held = DioramaInput.BagHeld();
            if (!held) _needRelease = false;

            if (!_open)
            {
                if (held && !_needRelease && CanOpen()) Open();
                return;
            }
            if (!held || health.IsDead)
            {
                Close(false);
                return;
            }
            Tick();
        }

        void LateUpdate()
        {
            if (_open && combat.body != null)
                combat.body.localRotation = _bodyAtOpen * Quaternion.Euler(leanDegrees, 0f, 0f);
        }

        void OnDisable()
        {
            if (_open) Close(false);
        }

        bool CanOpen()
        {
            if (Inventory.MenuOpen || health.IsDead || !combat.CanAct) return false;
            return !(hero != null && hero.dialogue != null && hero.dialogue.IsOpen);
        }

        // ------------------------------------------------------------------------------------------ open and close

        void Open()
        {
            _open = true;
            Inventory.MenuOpen = true;
            combat.InputLocked = true;
            _cursor = 0;
            if (combat.body != null) _bodyAtOpen = combat.body.localRotation;
            _noiseTimer = noiseEvery;

            BuildCloth();
            EnsurePanel();
            _panel.SetActive(true);
            inventory.Changed += OnChanged;
            health.Damaged += OnHit;
            Refresh();

            Time.timeScale = slowScale;
            NoiseBus.Emit(transform.position, noiseRadius);
        }

        void Close(bool forced)
        {
            _open = false;
            _needRelease = forced;
            Inventory.MenuOpen = false;
            combat.InputLocked = false;
            Time.timeScale = 1f;
            inventory.Changed -= OnChanged;
            health.Damaged -= OnHit;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
            ClearItems();
        }

        void OnHit(float amount, Vector2 dir)
        {
            if (_open) Close(true);
        }

        void OnChanged()
        {
            if (_open) Refresh();
        }

        // ------------------------------------------------------------------------------------------ input

        void Tick()
        {
            Time.timeScale = slowScale;

            Vector2Int step = DioramaInput.MenuStep();
            if (step != Vector2Int.zero) MoveCursor(step);

            int pocket = DioramaInput.BeltPressed();
            if (pocket >= 0)
            {
                if (_cursor < inventory.Bag.Count) inventory.AssignToBelt(_cursor, pocket);
                else inventory.Unequip(pocket);
            }

            if (DioramaInput.LightPressed() && _cursor < inventory.Bag.Count) inventory.UseBag(_cursor);

            _noiseTimer -= Time.unscaledDeltaTime;
            if (_noiseTimer <= 0f)
            {
                _noiseTimer = noiseEvery;
                NoiseBus.Emit(transform.position, noiseRadius);
            }
        }

        void MoveCursor(Vector2Int step)
        {
            int col = Mathf.Clamp(_cursor % columns + step.x, 0, columns - 1);
            int row = Mathf.Clamp(_cursor / columns - step.y, 0, Rows - 1);
            int idx = row * columns + col;
            if (idx >= inventory.BagCapacity) return;
            _cursor = idx;
            Refresh();
        }

        // ------------------------------------------------------------------------------------------ the cloth in the world

        Vector3 CellLocal(int index)
        {
            int col = index % columns;
            int row = index / columns;
            return new Vector3((col - (columns - 1) * 0.5f) * cellSize, 0f, ((Rows - 1) * 0.5f - row) * cellSize);
        }

        GameObject Cube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, PrimitiveType shape)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        void BuildCloth()
        {
            if (_root == null)
            {
                _root = new GameObject("Bag Cloth").transform;
                float w = columns * cellSize + 0.3f;
                float d = Rows * cellSize + 0.3f;
                Cube(_root, "Cloth", new Vector3(0f, 0.02f, 0f), new Vector3(w, 0.04f, d), clothMaterial, PrimitiveType.Cube);
                for (int i = 0; i < inventory.BagCapacity; i++)
                    Cube(_root, "Space " + i, CellLocal(i) + new Vector3(0f, 0.045f, 0f), new Vector3(cellSize * 0.88f, 0.01f, cellSize * 0.88f), cellMaterial, PrimitiveType.Cube);
                _cursorT = Cube(_root, "Cursor", Vector3.zero, new Vector3(cellSize * 0.98f, 0.01f, cellSize * 0.98f), cursorMaterial, PrimitiveType.Cube).transform;
            }

            // Below the hero on screen: away from the camera's far side, towards the camera.
            Quaternion rot = Quaternion.Euler(0f, clothYaw, 0f);
            Vector3 down = rot * Vector3.back;
            float distance = Rows * cellSize * 0.5f + 0.65f;
            Vector3 center = transform.position + down * distance;
            center.y = 0f;
            _root.SetPositionAndRotation(center, rot);
            _root.gameObject.SetActive(true);
        }

        void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null) Destroy(_items[i]);
            _items.Clear();
        }

        // Rebuilds the objects on the cloth and the label for the current cursor.
        void Refresh()
        {
            ClearItems();
            var bag = inventory.Bag;
            for (int i = 0; i < bag.Count; i++)
            {
                var stack = bag[i];
                var item = stack.item;
                Material mat = item.WorldMaterial != null ? item.WorldMaterial : cursorMaterial;
                int copies = Mathf.Min(stack.count, 3);
                for (int c = 0; c < copies; c++)
                {
                    float k = cellSize / 0.5f; // items are authored for a 0.5 cell
                    Vector3 size = item.WorldSize * k;
                    Vector3 pos = CellLocal(i) + new Vector3((0.05f * c - 0.04f) * k, 0.05f + size.y * 0.5f, (-0.04f * c + 0.02f) * k);
                    _items.Add(Cube(_root, item.DisplayName, pos, size, mat, item.WorldShape));
                }
            }
            if (_cursorT != null) _cursorT.localPosition = CellLocal(_cursor) + new Vector3(0f, 0.042f, 0f);

            if (_cursor < bag.Count)
            {
                var s = bag[_cursor];
                _title.text = s.item.DisplayName + "   x" + s.count;
                string desc = string.IsNullOrEmpty(s.item.Description) ? string.Empty : "  " + s.item.Description;
                _detail.text = s.item.EffectLine + desc;
            }
            else
            {
                _title.text = "Espacio libre";
                _detail.text = "A S D F devuelve ese bolsillo del cinturón al morral";
            }
        }

        // ------------------------------------------------------------------------------------------ label

        void EnsurePanel()
        {
            if (_panel != null || canvasRoot == null) return;
            var bg = UiKit.Img(canvasRoot, "Bag Panel", UiKit.PanelBg);
            _panel = bg.gameObject;
            UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(520f, 74f));
            _title = UiKit.Txt(bg.transform, "Title", 18, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(496f, 22f));
            _detail = UiKit.Txt(bg.transform, "Detail", 13, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            UiKit.Place(_detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -31f), new Vector2(496f, 18f));
            var hint = UiKit.Txt(bg.transform, "Hint", 12, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            UiKit.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -52f), new Vector2(496f, 18f));
            hint.text = "Flechas: elegir   Z: usar   A S D F: al cinturón   Soltar Tab: guardar";
            _panel.SetActive(false);
        }
    }
}
