using System.Collections.Generic;
using UnityEngine;

namespace WarehouseDuel.Core
{
    /// <summary>
    /// Pools short-lived effect prefabs (muzzle flashes, impacts, decals) so a match never calls
    /// Instantiate/Destroy for effects after warm-up. Each instance returns itself after <see cref="PooledEffect.lifetime"/>.
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        private static EffectPool instance;

        private readonly Dictionary<GameObject, Queue<PooledEffect>> available = new Dictionary<GameObject, Queue<PooledEffect>>();

        private static EffectPool Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Effect Pool").AddComponent<EffectPool>();
                return instance;
            }
        }

        /// <summary>Pre-creates <paramref name="count"/> instances so the first shots don't hitch.</summary>
        public static void Warm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var created = new List<PooledEffect>(count);
            for (int i = 0; i < count; i++) created.Add(Instance.Take(prefab));
            foreach (PooledEffect effect in created) effect.ReturnNow();
        }

        public static PooledEffect Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform follow = null)
        {
            if (prefab == null) return null;
            PooledEffect effect = Instance.Take(prefab);
            Transform t = effect.transform;
            t.SetParent(follow, false);
            t.localScale = Vector3.one;
            if (follow == null) t.SetPositionAndRotation(position, rotation);
            else
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
            effect.Play();
            return effect;
        }

        private PooledEffect Take(GameObject prefab)
        {
            if (!available.TryGetValue(prefab, out Queue<PooledEffect> queue))
            {
                queue = new Queue<PooledEffect>();
                available[prefab] = queue;
            }

            while (queue.Count > 0)
            {
                PooledEffect pooled = queue.Dequeue();
                if (pooled != null) return pooled; // may have been destroyed with a parent
            }

            GameObject go = Instantiate(prefab, transform);
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) effect = go.AddComponent<PooledEffect>();
            effect.Bind(this, prefab);
            go.SetActive(false);
            return effect;
        }

        internal void Return(PooledEffect effect, GameObject prefab)
        {
            if (effect == null) return;
            effect.gameObject.SetActive(false);
            effect.transform.SetParent(transform, false);
            available[prefab].Enqueue(effect);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
