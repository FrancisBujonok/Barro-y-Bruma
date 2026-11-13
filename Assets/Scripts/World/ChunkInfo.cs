using BrumaPx.Rendering;
using BrumaPx.World;
using BrumaPx.Player;
using BrumaPx.Dialogue;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>Marks a chunk root and stores its world-space centre (the root itself sits at the origin).</summary>
    public sealed class ChunkInfo : MonoBehaviour
    {
        public Vector3 center;
    }
}
