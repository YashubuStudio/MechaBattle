using System;
namespace MechaBattle.Contracts
{
    [Serializable] public sealed class MapCatalog
    {
        public int schemaVersion = 1;
        public MapCatalogEntry[] entries = Array.Empty<MapCatalogEntry>();
    }
    [Serializable] public sealed class MapCatalogEntry
    {
        public string id = "", title = "", description = "", resourcePath = "", theme = "", sourceFile = "";
    }
}
