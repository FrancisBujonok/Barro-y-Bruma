using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.Player;
using BrumaPx.World;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.UI
{
    /// <summary>
    /// The hero's notebook (N). One page per skill branch; every skill is a note on the page tied to the ones it needs
    /// with thread. It can be read anywhere, but points are only spent inside a refuge. Opening it pauses the game.
    /// For now the example branch has blank notes ("???") and nothing earns points yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotebookView : MonoBehaviour
    {
        [Header("Links")]
        public SkillBranchDef[] branches = new SkillBranchDef[0];
        public HeroProgress progress;
        public HeroCombat combat;
        public Health health;
        public Transform heroTransform;
        [Tooltip("The UI canvas that receives the notebook.")]
        public Transform canvasRoot;

        const float PageW = 596f, PageH = 290f, Margin = 34f, NodeSize = 46f;

        sealed class NodeView
        {
            public SkillNode node;
            public Image frame, fill;
            public Text label;
            public RectTransform rt;
            public Vector2 spot; // pixels on the page, y up
        }

        static readonly Color PageColor = new Color32(0x24, 0x1E, 0x19, 255);
        static readonly Color Blank = new Color32(0x3A, 0x33, 0x2B, 255);
        static readonly Color Thread = new Color32(0x6B, 0x5C, 0x4A, 255);

        bool _open;
        int _branch, _selected;
        GameObject _panel;
        RectTransform _page;
        Text _detail, _footer;
        readonly List<Image> _tabFrames = new List<Image>();
        readonly List<Text> _tabTexts = new List<Text>();
        readonly List<NodeView> _nodes = new List<NodeView>();
        readonly List<GameObject> _threads = new List<GameObject>();

        public bool IsOpen { get { return _open; } }

        void Start()
        {
            if (canvasRoot == null) return;
            BuildPanel();
            if (progress != null) progress.Changed += OnProgress;
        }

        void OnDestroy()
        {
            if (progress != null) progress.Changed -= OnProgress;
        }

        void OnDisable()
        {
            if (_open) Close();
        }

        void Update()
        {
            if (_panel == null || combat == null || health == null) return;

            if (!_open)
            {
                bool pressed = DioramaInput.NotebookPressed();
                if (pressed && !Inventory.MenuOpen && !health.IsDead && !Talking()) Open();
                return;
            }

            if (DioramaInput.NotebookPressed() || DioramaInput.CancelPressed() || health.IsDead)
            {
                Close();
                return;
            }
            Tick();
        }

        bool Talking()
        {
            return combat.hero != null && combat.hero.dialogue != null && combat.hero.dialogue.IsOpen;
        }

        // ------------------------------------------------------------------------------------------ open and close

        void Open()
        {
            _open = true;
            Inventory.MenuOpen = true;
            combat.InputLocked = true;
            Time.timeScale = 0f;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            ShowBranch(_branch);
        }

        void Close()
        {
            _open = false;
            Inventory.MenuOpen = false;
            combat.InputLocked = false;
            Time.timeScale = 1f;
            if (_panel != null) _panel.SetActive(false);
        }

        void Tick()
        {
            Time.timeScale = 0f;

            Vector2Int step = DioramaInput.MenuStep();
            if (step != Vector2Int.zero) MoveSelection(step);

            int page = DioramaInput.PageStep();
            if (page != 0 && branches.Length > 1) ShowBranch((_branch + page + branches.Length) % branches.Length);

            if (DioramaInput.AdvancePressed()) TryLearn();
            RefreshTexts();
        }

        void OnProgress()
        {
            if (_open) Refresh();
        }

        // ------------------------------------------------------------------------------------------ layout

        void BuildPanel()
        {
            var bg = UiKit.Img(canvasRoot, "Notebook", UiKit.Border);
            _panel = bg.gameObject;
            UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(792f, 432f));
            var inner = UiKit.Img(bg.transform, "Paper", UiKit.PanelBg);
            UiKit.Stretch(inner.rectTransform, 3f, 3f, 3f, 3f);

            var title = UiKit.Txt(inner.transform, "Title", 20, FontStyle.Bold, UiKit.Warm, TextAnchor.UpperLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(300f, 28f));
            title.text = "Cuaderno";

            for (int i = 0; i < branches.Length; i++)
            {
                var tab = UiKit.Img(inner.transform, "Tab " + i, UiKit.PanelBg);
                UiKit.Place(tab.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -52f - i * 34f), new Vector2(142f, 28f));
                var t = UiKit.Txt(tab.transform, "Label", 14, FontStyle.Bold, UiKit.Cream, TextAnchor.MiddleLeft);
                UiKit.Stretch(t.rectTransform, 10f, 0f, 4f, 0f);
                t.text = branches[i] != null ? branches[i].DisplayName : "?";
                _tabFrames.Add(tab);
                _tabTexts.Add(t);
            }

            var page = UiKit.Img(inner.transform, "Page", PageColor);
            _page = page.rectTransform;
            UiKit.Place(_page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -48f), new Vector2(PageW, PageH));

            _detail = UiKit.Txt(inner.transform, "Detail", 14, FontStyle.Normal, UiKit.Cream, TextAnchor.UpperLeft);
            UiKit.Place(_detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -346f), new Vector2(PageW, 40f));

            _footer = UiKit.Txt(inner.transform, "Footer", 13, FontStyle.Normal, UiKit.Mid, TextAnchor.UpperLeft);
            UiKit.Place(_footer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -392f), new Vector2(760f, 36f));

            _panel.SetActive(false);
        }

        void ShowBranch(int index)
        {
            _branch = Mathf.Clamp(index, 0, Mathf.Max(0, branches.Length - 1));
            for (int i = _page.childCount - 1; i >= 0; i--) Destroy(_page.GetChild(i).gameObject);
            _nodes.Clear();
            _threads.Clear();
            _selected = 0;

            var def = branches.Length > 0 ? branches[_branch] : null;
            if (def != null)
            {
                var list = def.Nodes;
                var byId = new Dictionary<string, NodeView>();
                // Threads first so the notes sit on top of them.
                var threadLayer = new GameObject("Threads", typeof(RectTransform)).GetComponent<RectTransform>();
                threadLayer.SetParent(_page, false);
                UiKit.Stretch(threadLayer, 0f, 0f, 0f, 0f);

                for (int i = 0; i < list.Count; i++)
                {
                    var nv = new NodeView { node = list[i] };
                    nv.spot = new Vector2(list[i].position.x * (PageW - 2f * Margin), list[i].position.y * (PageH - 2f * Margin));
                    byId[list[i].id] = nv;
                    _nodes.Add(nv);
                }
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var req = _nodes[i].node.requires;
                    if (req == null) continue;
                    for (int r = 0; r < req.Length; r++)
                    {
                        NodeView from;
                        if (byId.TryGetValue(req[r], out from)) AddThread(threadLayer, from, _nodes[i]);
                    }
                }
                for (int i = 0; i < _nodes.Count; i++) BuildNode(_nodes[i]);
            }
            Refresh();
        }

        static Vector2 OnPage(NodeView n)
        {
            return new Vector2(Margin + n.spot.x, -(Margin + (PageH - 2f * Margin) - n.spot.y));
        }

        void AddThread(RectTransform layer, NodeView a, NodeView b)
        {
            Vector2 pa = OnPage(a), pb = OnPage(b);
            var line = UiKit.Img(layer, "Thread", Thread);
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            Vector2 d = pb - pa;
            rt.anchoredPosition = pa;
            rt.sizeDelta = new Vector2(d.magnitude, 3f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            _threads.Add(line.gameObject);
        }

        void BuildNode(NodeView n)
        {
            n.frame = UiKit.Img(_page, "Note " + n.node.id, UiKit.Border);
            n.rt = n.frame.rectTransform;
            n.rt.anchorMin = n.rt.anchorMax = new Vector2(0f, 1f);
            n.rt.pivot = new Vector2(0.5f, 0.5f);
            n.rt.anchoredPosition = OnPage(n);
            n.rt.sizeDelta = new Vector2(NodeSize, NodeSize);
            n.fill = UiKit.Img(n.frame.transform, "Fill", Blank);
            UiKit.Stretch(n.fill.rectTransform, 3f, 3f, 3f, 3f);
            n.label = UiKit.Txt(n.fill.transform, "Label", 15, FontStyle.Bold, UiKit.Mid, TextAnchor.MiddleCenter);
            UiKit.Stretch(n.label.rectTransform, 0f, 0f, 0f, 0f);
            n.label.text = "?";
        }

        // ------------------------------------------------------------------------------------------ state

        enum NodeState { Learned, Available, Locked }

        NodeState StateOf(SkillNode node)
        {
            if (progress != null && progress.IsLearned(node.id)) return NodeState.Learned;
            if (progress == null || progress.PrerequisitesMet(node)) return NodeState.Available;
            return NodeState.Locked;
        }

        void Refresh()
        {
            for (int i = 0; i < _tabFrames.Count; i++)
            {
                bool on = i == _branch;
                _tabFrames[i].color = on ? UiKit.Border : UiKit.PanelBg;
                _tabTexts[i].color = on ? UiKit.Warm : UiKit.Cream;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                bool sel = i == _selected;
                NodeState s = StateOf(n.node);
                n.frame.color = sel ? UiKit.Warm : UiKit.Border;
                n.rt.localScale = sel ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
                n.fill.color = s == NodeState.Learned ? UiKit.Warm : (s == NodeState.Available ? Blank : UiKit.PanelBg);
                n.label.color = s == NodeState.Learned ? UiKit.PanelBg : (s == NodeState.Available ? UiKit.Cream : UiKit.Mid);
                n.label.text = n.node.title.Length > 0 ? (n.node.title == "???" ? "?" : n.node.title.Substring(0, 1)) : "?";
            }
            RefreshTexts();
        }

        void RefreshTexts()
        {
            if (_detail == null) return;
            if (_selected >= 0 && _selected < _nodes.Count)
            {
                var node = _nodes[_selected].node;
                string status;
                switch (StateOf(node))
                {
                    case NodeState.Learned: status = "Aprendida"; break;
                    case NodeState.Available: status = "Costo " + node.cost + (progress != null && progress.Points >= node.cost ? "" : " (faltan puntos)"); break;
                    default: status = "Bloqueada: antes hay que aprender otras notas"; break;
                }
                string text = string.IsNullOrEmpty(node.description) ? "Página en blanco por ahora." : node.description;
                _detail.text = node.title + "   " + status + "\n" + text;
            }
            else
            {
                _detail.text = "Esta rama está vacía.";
            }

            Refuge refuge = heroTransform != null ? Refuge.At(heroTransform.position) : null;
            string points = "Puntos: " + (progress != null ? progress.Points : 0) + "     ";
            string where = refuge != null
                ? "Estás en " + refuge.DisplayName + ": Enter o E aprende la nota elegida"
                : "Descansa en un refugio para gastarlos";
            _footer.text = points + where + "\nFlechas: elegir nota   Q W: cambiar de página   N o Esc: cerrar";
        }

        void MoveSelection(Vector2Int step)
        {
            if (_nodes.Count == 0) return;
            Vector2 dir = new Vector2(step.x, step.y).normalized;
            Vector2 here = _nodes[_selected].spot;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (i == _selected) continue;
                Vector2 d = _nodes[i].spot - here;
                float dist = d.magnitude;
                if (dist < 0.01f) continue;
                float dot = Vector2.Dot(d / dist, dir);
                if (dot < 0.3f) continue;
                float score = dist / dot;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) return;
            _selected = best;
            Refresh();
        }

        void TryLearn()
        {
            if (progress == null || _selected < 0 || _selected >= _nodes.Count) return;
            if (heroTransform == null || Refuge.At(heroTransform.position) == null) return;
            progress.TryLearn(_nodes[_selected].node);
        }
    }
}
