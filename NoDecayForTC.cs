using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Decay For TC", "YourName", "1.0.0")]
    [Description("Prevents decay of buildings within the Tool Cupboard's area.")]
    public class NoDecayForTC : RustPlugin
    {
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            // Проверяем, является ли сущность строением
            if (entity is BuildingBlock building)
            {
                // Проверяем материал здания
                if (building.grade == BuildingGrade.Enum.TopTier) // TopTier = МВК
                {
                    // Проверяем, есть ли шкаф, охватывающий здание
                    BuildingPrivlidge tc = building.GetBuildingPrivilege();
                    if (tc != null)
                    {
                        // Отменяем урон от гниения
                        if (info.damageTypes.Has(Rust.DamageType.Decay))
                        {
                            info.damageTypes.Scale(Rust.DamageType.Decay, 0f);
                        }
                    }
                }
            }
        }
    }
}
