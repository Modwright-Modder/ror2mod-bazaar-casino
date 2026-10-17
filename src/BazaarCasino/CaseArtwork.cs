using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal static class CaseArtwork
    {
        private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> images = new Dictionary<string, AsyncOperationHandle<Sprite>>();
        internal static void Assign(Image target, ItemDef item)
        {
            if (!item)
            {
                target.enabled = false;
                return;
            }
#pragma warning disable CS0618 // Legacy modded ItemDefs may still set their sprite directly.
            if (item.pickupIconSprite)
            {
                target.sprite = item.pickupIconSprite;
                return;
            }
#pragma warning restore CS0618
            var reference = item.pickupIconSpriteReference;
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                target.enabled = false;
                return;
            }
            if (!images.TryGetValue(item.name, out var request))
            {
                request = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
                images[item.name] = request;
            }
            if (request.IsDone)
            {
                target.sprite = request.Result;
                target.enabled = target.sprite;
            }
            else
            {
                target.enabled = false;
                request.Completed += loaded => { if (target) { target.sprite = loaded.Status == AsyncOperationStatus.Succeeded ? loaded.Result : null; target.enabled = target.sprite; } };
            }
        }
        internal static void Release()
        {
            foreach (var request in images.Values)
                if (request.IsValid())
                    Addressables.Release(request);
            images.Clear();
        }
    }
}

