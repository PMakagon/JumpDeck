using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    internal static class JumpDeckService
    {
        internal static int AddObjects(
            JumpDeckData data,
            JumpDeckCollection deck,
            IEnumerable<Object> objects,
            out List<string> errors)
        {
            errors = new List<string>();
            var additions = new List<JumpPin>();
            var existingIdentities = new HashSet<string>(
                deck.Pins
                    .Where(pin => pin.Kind == JumpPinKind.Object)
                    .Select(JumpDeckObjectUtility.BuildIdentity));

            foreach (Object value in objects.Where(value => value != null).Distinct())
            {
                if (!JumpDeckObjectUtility.TryCreatePin(value, out JumpPin pin, out string error))
                {
                    if (!string.IsNullOrEmpty(error))
                        errors.Add(error);
                    continue;
                }

                string identity = JumpDeckObjectUtility.BuildIdentity(pin);
                if (!existingIdentities.Add(identity))
                    continue;

                additions.Add(pin);
            }

            if (additions.Count == 0)
                return 0;

            Undo.RecordObject(data, "Add JumpDeck Pins");
            deck.Pins.AddRange(additions);
            deck.Expanded = true;
            data.SaveData();
            return additions.Count;
        }
    }
}
