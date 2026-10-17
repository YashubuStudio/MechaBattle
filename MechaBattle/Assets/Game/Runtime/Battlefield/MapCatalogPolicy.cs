using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MechaBattle.Contracts;
namespace MechaBattle.Battlefield
{
    public static class MapCatalogPolicy
    {
        public static MapDefinition[] ValidateAndLoad(MapCatalog catalog, Func<string, MapDefinition> load)
        {
            if(catalog==null || catalog.schemaVersion!=1 || catalog.entries==null || catalog.entries.Length==0)throw new ArgumentException("Invalid map catalog.");
            if(load==null)throw new ArgumentNullException(nameof(load));
            var ids=new HashSet<string>(StringComparer.Ordinal);var paths=new HashSet<string>(StringComparer.Ordinal);var maps=new MapDefinition[catalog.entries.Length];
            for(int i=0;i<maps.Length;i++)
            {
                var entry=catalog.entries[i];
                if(entry==null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id) || string.IsNullOrWhiteSpace(entry.title)
                    || string.IsNullOrWhiteSpace(entry.description) || !Regex.IsMatch(entry.resourcePath??"","^MechaBattleMaps/[A-Za-z0-9]+$") || !paths.Add(entry.resourcePath)
                    || !Regex.IsMatch(entry.sourceFile??"","^[a-z0-9-]+[.]json$") || (entry.theme!="industrial" && entry.theme!="works" && entry.theme!="canyon" && entry.theme!="urban"))
                    throw new ArgumentException("Invalid or duplicate map entry: "+i);
                var map=load(entry.resourcePath);MapValidator.ThrowIfInvalid(map);
                if(map.id!=entry.id)throw new ArgumentException("Map ID mismatch: "+entry.id);
                maps[i]=map;
            }
            return maps;
        }
    }
}
