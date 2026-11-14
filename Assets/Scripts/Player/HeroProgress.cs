using System;
using System.Collections.Generic;
using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// Skill points and which skill nodes the hero has learned. Nothing earns points yet and no node does anything yet;
    /// this only holds the state the notebook shows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroProgress : MonoBehaviour
    {
        [SerializeField, Min(0)] int points;

        readonly HashSet<string> _learned = new HashSet<string>();

        public int Points { get { return points; } }
        public event Action Changed;

        public bool IsLearned(string id)
        {
            return _learned.Contains(id);
        }

        public bool PrerequisitesMet(SkillNode node)
        {
            if (node.requires == null) return true;
            for (int i = 0; i < node.requires.Length; i++)
                if (!_learned.Contains(node.requires[i])) return false;
            return true;
        }

        public bool CanLearn(SkillNode node)
        {
            return node != null && !_learned.Contains(node.id) && points >= node.cost && PrerequisitesMet(node);
        }

        public bool TryLearn(SkillNode node)
        {
            if (!CanLearn(node)) return false;
            points -= node.cost;
            _learned.Add(node.id);
            if (Changed != null) Changed();
            return true;
        }

        public void AddPoints(int amount)
        {
            points = Mathf.Max(0, points + amount);
            if (Changed != null) Changed();
        }
    }
}
