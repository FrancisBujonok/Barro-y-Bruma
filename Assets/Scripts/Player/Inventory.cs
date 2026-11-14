using System;
using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>A pile of one kind of item.</summary>
    public sealed class ItemStack
    {
        public ItemDef item;
        public int count;
    }

    /// <summary>
    /// What the hero carries. Two places, on purpose: the belt has four pockets (A, S, D, F) that are used at once,
    /// and the bag has twelve spaces that can only be reached by laying it out on the ground (see BagView).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Inventory : MonoBehaviour
    {
        public const int BeltSize = 4;

        [Serializable]
        public sealed class StartItem
        {
            public ItemDef item;
            [Min(1)] public int count = 1;
        }

        [Header("Links")]
        public Health health;
        public HeroCombat combat;

        [Header("Bag")]
        [SerializeField, Min(1)] int bagCapacity = 12;
        [Tooltip("What the hero carries when the game starts (test items for now).")]
        [SerializeField] StartItem[] startingItems = new StartItem[0];

        [Header("Use")]
        [SerializeField] float useCooldown = 0.35f;
        [SerializeField] float useNoise = 2.5f;

        /// <summary>True while the bag or the notebook is open; belt letters mean something else then.</summary>
        public static bool MenuOpen;

        public event Action Changed;

        readonly ItemStack[] _belt = new ItemStack[BeltSize];
        readonly List<ItemStack> _bag = new List<ItemStack>();
        float _nextUse;

        public int BagCapacity { get { return bagCapacity; } }
        public IReadOnlyList<ItemStack> Bag { get { return _bag; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MenuOpen = false;
        }

        void Start()
        {
            for (int i = 0; i < startingItems.Length; i++)
                if (startingItems[i] != null && startingItems[i].item != null)
                    Add(startingItems[i].item, startingItems[i].count);
        }

        void Update()
        {
            if (MenuOpen || health == null || health.IsDead) return;
            int slot = DioramaInput.BeltPressed();
            if (slot >= 0) UseBelt(slot);
        }

        public ItemStack GetBelt(int slot)
        {
            return slot >= 0 && slot < BeltSize ? _belt[slot] : null;
        }

        /// <summary>Puts items away. Returns how many did not fit.</summary>
        public int Add(ItemDef item, int count)
        {
            if (item == null || count <= 0) return count;

            for (int i = 0; i < BeltSize && count > 0; i++)
                count = Fill(_belt[i], item, count);
            for (int i = 0; i < _bag.Count && count > 0; i++)
                count = Fill(_bag[i], item, count);
            while (count > 0 && _bag.Count < bagCapacity)
            {
                int put = Mathf.Min(count, item.MaxStack);
                _bag.Add(new ItemStack { item = item, count = put });
                count -= put;
            }
            RaiseChanged();
            return count;
        }

        static int Fill(ItemStack stack, ItemDef item, int count)
        {
            if (stack == null || stack.item != item) return count;
            int room = item.MaxStack - stack.count;
            int put = Mathf.Min(room, count);
            stack.count += put;
            return count - put;
        }

        /// <summary>Moves a bag stack into a belt pocket. A pocket that was busy swaps its pile back into the bag.</summary>
        public bool AssignToBelt(int bagIndex, int slot)
        {
            if (bagIndex < 0 || bagIndex >= _bag.Count || slot < 0 || slot >= BeltSize) return false;
            ItemStack incoming = _bag[bagIndex];
            ItemStack current = _belt[slot];

            if (current != null && current.item == incoming.item)
            {
                int put = Mathf.Min(incoming.item.MaxStack - current.count, incoming.count);
                current.count += put;
                incoming.count -= put;
                if (incoming.count <= 0) _bag.RemoveAt(bagIndex);
            }
            else
            {
                _belt[slot] = incoming;
                if (current != null) _bag[bagIndex] = current;
                else _bag.RemoveAt(bagIndex);
            }
            RaiseChanged();
            return true;
        }

        /// <summary>Takes a belt pile back into the bag.</summary>
        public bool Unequip(int slot)
        {
            if (slot < 0 || slot >= BeltSize || _belt[slot] == null) return false;
            if (_bag.Count >= bagCapacity) return false;
            _bag.Add(_belt[slot]);
            _belt[slot] = null;
            RaiseChanged();
            return true;
        }

        public bool UseBelt(int slot)
        {
            ItemStack s = GetBelt(slot);
            if (!TryApply(s)) return false;
            if (s.count <= 0) _belt[slot] = null;
            RaiseChanged();
            return true;
        }

        public bool UseBag(int index)
        {
            if (index < 0 || index >= _bag.Count) return false;
            ItemStack s = _bag[index];
            if (!TryApply(s)) return false;
            if (s.count <= 0) _bag.RemoveAt(index);
            RaiseChanged();
            return true;
        }

        // Uses one item of the pile if its effect would do anything right now.
        bool TryApply(ItemStack s)
        {
            if (s == null || s.item == null || s.count <= 0 || !s.item.IsUsable) return false;
            if (Time.unscaledTime < _nextUse) return false;
            if (health == null || health.IsDead) return false;
            if (combat != null && !combat.CanAct) return false;

            switch (s.item.Effect)
            {
                case ItemEffect.Heal:
                    if (health.Current >= health.max - 0.01f) return false;
                    health.Heal(s.item.Amount);
                    break;
                case ItemEffect.RestoreStamina:
                    if (combat == null || combat.Stamina01 >= 0.999f) return false;
                    combat.RestoreStamina(s.item.Amount);
                    break;
                default:
                    return false;
            }

            s.count--;
            _nextUse = Time.unscaledTime + useCooldown;
            NoiseBus.Emit(transform.position, useNoise);
            return true;
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
        }
    }
}
