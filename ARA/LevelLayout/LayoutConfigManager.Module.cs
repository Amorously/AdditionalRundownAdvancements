using AmorLib.Utils.Extensions;
using ARA.LevelLayout.DefinitionData;
using FluffyUnderware.DevTools.Extensions;
using LevelGeneration;
using UnityEngine;

namespace ARA.LevelLayout;

public partial class LayoutConfigManager
{
    private static void ApplyLayoutData()
    {
        foreach (var zone in Builder.CurrentFloor.allZones)
        {
            /* Setup All WE Terminals */
            if (Current.AllWorldEventTerminals) 
            {
                SetupAllWETerminals(zone);
            }
            
            if (!TryGetCurrentZoneData(zone, out var zoneData) || zoneData?.Zone == null) continue;

            /* Add Spawnpoints to Zone Areas */
            zoneData.AddSpawnPoints();

            /* Add Custom WE Objects */
            foreach (var weData in zoneData.WorldEventObjects)
            {
                if (!weData.IsAreaIndexValid(zone, out var area)) continue;
                if (!weData.UseExistingFilterInArea || !TryGetWorldEventInArea(weData.WorldEventObjectFilter, area, out var weObj))
                {
                    weObj = area.AddChildGameObject<LG_WorldEventObject>(weData.WorldEventObjectFilter);
                    if (weData.UseRandomPosition) weData.Position = area.m_courseNode.GetRandomPositionInside();
                    weObj.transform.SetPositionAndRotation(weData.Position, Quaternion.Euler(weData.Rotation));
                    weObj.transform.localScale = weData.Scale;
                    weObj.WorldEventComponents = Array.Empty<IWorldEventComponent>();
                }

                /* Setup Custom WE Component(s) */
                foreach ((var type, var weComp) in weData.Components)
                {
                    switch (type)
                    {                        
                        case WorldEventComponent.WE_SpecificTerminal when weComp.PrefabOverride != TerminalPrefab.None:
                        case WorldEventComponent.WE_SpecificPickup:
                            _positionToContainerMap[weData.Position] = new(weData.WorldEventObjectFilter, area.m_courseNode, weComp.PrefabOverride, weComp.EventsOnPickup);
                            break;

                        case WorldEventComponent.WE_ChainedPuzzle:
                            weObj.gameObject.AddOrGetComponent<LG_WorldEventChainPuzzle>();
                            break;

                        case WorldEventComponent.WE_NavMarker:
                            var weNav = weObj.gameObject.AddOrGetComponent<PlaceNavMarkerOnGO>();
                            weNav.type = weComp.NavMarkerType;
                            weNav.m_placeOnStart = weComp.PlaceOnStart;
                            weObj.gameObject.AddOrGetComponent<LG_WorldEventNavMarker>();
                            break;

                        case WorldEventComponent.WE_CollisionTrigger:
                        case WorldEventComponent.WE_LookatTrigger:
                        case WorldEventComponent.WE_InteractTrigger:
                            break;
                    }
                }
            }
        }
    }    

    private static void SetupAllWETerminals(LG_Zone zone)
    {
        for (int i = 0; i < zone.TerminalsSpawnedInZone.Count; i++)
        {
            var term = zone.TerminalsSpawnedInZone[i];
            var parentMarker = term.GetComponentInParent<LG_MarkerProducer>();
            if (parentMarker == null) continue;
            string name = $"WE_ARA_Term_{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_{i}";
            var weTerm = parentMarker.AddChildGameObject<LG_WorldEventObject>(name);
            weTerm.transform.localPosition = Vector3.zero;
            weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
        }

        if (zone.gameObject.TryAndGetComponent<LG_WardenObjective_Reactor>(out var reactor))
        {
            string name = $"WE_ARA_Term_{(int)zone.DimensionIndex}_{(int)zone.Layer.m_type}_{(int)zone.LocalIndex}_Reactor";
            var weTerm = reactor.m_terminalAlign?.AddChildGameObject<LG_WorldEventObject>(name);
            if (weTerm == null) return;
            weTerm.transform.localPosition = Vector3.zero;
            weTerm.WorldEventComponents = Array.Empty<IWorldEventComponent>();
        }
    }
}
