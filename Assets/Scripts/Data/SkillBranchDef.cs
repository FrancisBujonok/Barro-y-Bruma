using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>One node of a skill branch: a spot on the notebook page.</summary>
    [Serializable]
    public sealed class SkillNode
    {
        [Tooltip("Unique id across all branches, e.g. explorador.1")]
        public string id = "branch.1";
        public string title = "???";
        [TextArea(2, 4)] public string description = string.Empty;
        [Tooltip("Place on the page: x and y between 0 and 1, y = 1 at the top.")]
        public Vector2 position = new Vector2(0.5f, 0.5f);
        [Min(1)] public int cost = 1;
        [Tooltip("Ids of the nodes that must be learned first.")]
        public string[] requires = new string[0];
    }

    /// <summary>
    /// A branch of skills (one page of the notebook). Only the shape lives here for now; what each node does
    /// is added later. Create from the menu "Barro y Bruma/Skill Branch".
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Skill Branch", fileName = "NewBranch")]
    public sealed class SkillBranchDef : ScriptableObject
    {
        [SerializeField] string _displayName = "Rama";
        [SerializeField, TextArea(2, 4)] string _summary = string.Empty;
        [SerializeField] List<SkillNode> _nodes = new List<SkillNode>();

        public string DisplayName { get { return _displayName; } }
        public string Summary { get { return _summary; } }
        public IReadOnlyList<SkillNode> Nodes { get { return _nodes; } }

        public SkillNode Find(string id)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].id == id) return _nodes[i];
            return null;
        }
    }
}
