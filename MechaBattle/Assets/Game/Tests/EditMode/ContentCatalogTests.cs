using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
namespace MechaBattle.Tests
{
    public sealed class ContentCatalogTests
    {
        private static MapCatalog Catalog()=>new MapCatalog{entries=new[]{
            new MapCatalogEntry{id="one",title="First",description="Main and flanks",resourcePath="MechaBattleMaps/One",sourceFile="one-map.json",theme="industrial"},
            new MapCatalogEntry{id="two",title="Second",description="Terraces",resourcePath="MechaBattleMaps/Two",sourceFile="two-map.json",theme="works"}}};
        private static MapDefinition Load(string path)
        {var map=SimulationTests.Map();map.id=path.EndsWith("One",StringComparison.Ordinal)?"one":"two";return map;}
        [Test] public void CatalogLoadsDistinctValidatedMapsInDisplayOrder()
        {var maps=MapCatalogPolicy.ValidateAndLoad(Catalog(),Load);Assert.That(maps.Select(m=>m.id),Is.EqualTo(new[]{"one","two"}));Assert.That(maps[0],Is.Not.SameAs(maps[1]));}
        [TestCase("schema")] [TestCase("duplicate-id")] [TestCase("duplicate-path")] [TestCase("traversal")]
        [TestCase("title")] [TestCase("theme")] [TestCase("source")] [TestCase("mismatch")] [TestCase("one-base")]
        public void MalformedCatalogOrMapIsRejected(string failure)
        {
            var catalog=Catalog();if(failure=="schema")catalog.schemaVersion=2;
            if(failure=="duplicate-id")catalog.entries[1].id="one";
            if(failure=="duplicate-path")catalog.entries[1].resourcePath=catalog.entries[0].resourcePath;
            if(failure=="traversal")catalog.entries[0].resourcePath="MechaBattleMaps/../../Secret";
            if(failure=="title")catalog.entries[0].title=" ";if(failure=="theme")catalog.entries[0].theme="missing";
            if(failure=="source")catalog.entries[0].sourceFile="../outside.json";
            Assert.Throws<ArgumentException>(()=>MapCatalogPolicy.ValidateAndLoad(catalog,path=>{var map=Load(path);if(failure=="mismatch")map.id="wrong";if(failure=="one-base")map.mainBases=new[]{map.mainBases[0],map.mainBases[3]};return map;}));
        }
        [Test] public void PaintIdsAndTintsAreUniqueAndBounded()
        {
            Assert.That(BuildAppearance.PaintCount,Is.EqualTo(7));var ids=new System.Collections.Generic.HashSet<string>();var colors=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<BuildAppearance.PaintCount;i++)
            {var id=BuildAppearance.PaintId(i);Assert.That(ids.Add(id),Is.True);Assert.That(BuildAppearance.PaintIndex(id),Is.EqualTo(i));BuildAppearance.Tint(i,out float r,out float g,out float b);foreach(float value in new[]{r,g,b})Assert.That(value,Is.InRange(0,1));Assert.That(colors.Add(r+"/"+g+"/"+b),Is.True);}
            Assert.That(BuildAppearance.PaintIndex("missing"),Is.EqualTo(-1));
        }
        [TestCase(-1)] [TestCase(7)] public void InvalidPaintIndexCannotReadAnArbitraryColor(int index)
        {Assert.Throws<ArgumentOutOfRangeException>(()=>BuildAppearance.PaintId(index));Assert.Throws<ArgumentOutOfRangeException>(()=>BuildAppearance.Tint(index,out _,out _,out _));}
    }
}
