namespace Oxide.Plugins
{
    [Info("LBigBox", "Lore", "1.0.1")]
    internal class LBigBox : RustPlugin
    {
        private void OnServerInitialized()
        {
            var storages = UnityEngine.Object.FindObjectsOfType<StorageContainer>();

            foreach (StorageContainer storage in storages)
            {
                if (storage.name.Contains("woodbox_deployed"))
                {
                    OnEntitySpawned(storage);
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;

            if (entity is StorageContainer && entity.name.Contains("woodbox_deployed"))
            {
                StorageContainer storageContainer = entity as StorageContainer;

                if (storageContainer.panelName != "genericlarge")
                {
                    storageContainer.panelName = "genericlarge";

                    storageContainer.inventory.capacity = 18;

                    storageContainer.SendNetworkUpdate();
                }
            }
        }
    }
}