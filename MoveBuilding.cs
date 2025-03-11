using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MoveBuilding", "sdapro", "0.0.1")]
    public class MoveBuilding : RustPlugin
    {
        private const string PermissionMove = "Movebuilding.use";
        private BuildingBlock selectedBlock = null;
        private bool isMoving = false;

        private void Init() => permission.RegisterPermission(PermissionMove, this);

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionMove)) return;

            if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                if (!isMoving)
                {
                    TrySelectBuildingObject(player);
                }
                else
                {
                    PlaceBuildingObject();
                }
            }
            if (isMoving && selectedBlock != null)
            {
                MoveSelectedBuilding(player);
            }
        }

        private void TrySelectBuildingObject(BasePlayer player)
        {
            Ray ray = player.eyes.HeadRay();
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                BaseEntity entity = hit.GetEntity() as BaseEntity;
                if (entity is BuildingBlock buildingBlock)
                {
                    string blockName = buildingBlock.blockDefinition.info.name.english.ToLower();
                    if (blockName.Contains("foundation") || blockName.Contains("frame") || blockName.Contains("doorway") || blockName.Contains("low") || blockName.Contains("spiral") || blockName.Contains("stairs") || blockName.Contains("floor") || blockName.Contains("steps") || blockName.Contains("window") || blockName.Contains("half"))
                    {
                        player.ChatMessage("Only Walls!");
                        return;
                    }
                    selectedBlock = buildingBlock;
                    selectedBlock.SetFlag(BaseEntity.Flags.Busy, true);
                    isMoving = true;
                }
            }
        }

        private void MoveSelectedBuilding(BasePlayer player)
        { 
            if (selectedBlock == null) return;
            Vector3 newPosition = player.transform.position + player.eyes.BodyForward() * 3f;
            selectedBlock.transform.position = newPosition;
            selectedBlock.SetFlag(BaseEntity.Flags.Busy, true);
            selectedBlock.SendNetworkUpdateImmediate();
        }

        private void PlaceBuildingObject()
        {
            if (selectedBlock != null)
            {
                selectedBlock.SetFlag(BaseEntity.Flags.Busy, false);
                selectedBlock.SendNetworkUpdate();
                selectedBlock = null;
                isMoving = false;
            }
        }
    }
}
