using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.StyleTest
{
    /// <summary>
    /// Keeps only the chunks near the hero loaded. The fog island hides everything past its edge, so chunks are
    /// switched off just outside it. Runs in play mode only, so the saved scene always has every chunk enabled.
    /// </summary>
    public sealed class ChunkStreamer : MonoBehaviour
    {
        public Transform focus;
        public Transform chunkRoot;
        [Tooltip("Island radius (~10 on the diagonal) plus half a chunk diagonal (~8.5).")]
        public float activateRadius = 19f;
        public Text label;

        ChunkInfo[] _chunks;

        void Start()
        {
            if (chunkRoot != null) _chunks = chunkRoot.GetComponentsInChildren<ChunkInfo>(true);
        }

        void Update()
        {
            if (_chunks == null || focus == null) return;

            Vector3 f = focus.position;
            int active = 0;
            for (int i = 0; i < _chunks.Length; i++)
            {
                Vector3 d = _chunks[i].center - f;
                d.y = 0f;
                bool on = d.magnitude <= activateRadius;
                if (_chunks[i].gameObject.activeSelf != on) _chunks[i].gameObject.SetActive(on);
                if (on) active++;
            }

            if (label != null) label.text = "chunks cargados: " + active + " / " + _chunks.Length;
        }
    }
}
