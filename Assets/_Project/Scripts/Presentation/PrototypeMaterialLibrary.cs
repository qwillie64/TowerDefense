using UnityEngine;

namespace CyberWizard.Presentation
{
    public enum PrototypeMaterialRole
    {
        Map,
        Neutral,
        Player,
        Enemy
    }

    public static class PrototypeMaterialLibrary
    {
        public static Material Get(PrototypeMaterialRole role)
        {
            return Resources.Load<Material>($"PrototypePalette/{role}");
        }

        public static void Apply(GameObject target, PrototypeMaterialRole role, bool includeChildren = true)
        {
            if (target == null)
            {
                return;
            }

            var material = Get(role);
            if (material == null)
            {
                Debug.LogWarning($"找不到原型材質：{role}", target);
                return;
            }

            var renderers = includeChildren
                ? target.GetComponentsInChildren<Renderer>(true)
                : target.GetComponents<Renderer>();
            var targetIsHealthBar = target.GetComponentInParent<WorldHealthBar>() != null;

            for (var index = 0; index < renderers.Length; index++)
            {
                if (!targetIsHealthBar && renderers[index].GetComponentInParent<WorldHealthBar>() != null)
                {
                    continue;
                }

                renderers[index].sharedMaterial = material;
            }
        }
    }
}
