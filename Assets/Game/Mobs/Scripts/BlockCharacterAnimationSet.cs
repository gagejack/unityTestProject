using System;
using UnityEngine;

namespace Game.Mobs
{
    [CreateAssetMenu(menuName = "Block Characters/Animation Set")]
    public sealed class BlockCharacterAnimationSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string label;
            public string stateName;
            public AnimationClip clip;
        }

        public string characterName;
        public Entry[] animations = Array.Empty<Entry>();
    }
}
