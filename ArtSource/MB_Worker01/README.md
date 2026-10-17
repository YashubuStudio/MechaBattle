# MB-01 Modular Frame

Original MechaBattle prototype following UserText/MechaBattle機体構想・カスタマイズシステム構想案.md. Y up / +Z forward / metres / foot origin.

- 23 modules: four frames, three compatible left/right arm families, thirteen equipment modules. Ten ordinary saved builds; any valid three-equipment loadout uses the same kit.
- Three shared material slots: hull armor, dark frame, fixed team panels/sensors. Both shoulder armor surfaces, both knees and the head cap use the team panel slot regardless of selected hull color. Reinforced shoulder caps and mobile shoulders follow the same rule.
- COLOR-25 hull choices: team default, white, yellow, green, black, red and blue. Existing saved IDs Neutral/White/Sand/Olive/Graphite remain valid; red/blue use Red/Cobalt. The UI names Sand/Olive/Graphite now read イエロー/グリーン/ブラック.
- Blue team panels RGB(0.04,0.26,1), orange team RGB(1,0.36,0.015). Hull paint never changes these areas. Editor generation keeps selected hull paint when supplying a team palette. Runtime/Editor use BuildAppearance.TeamTint and shared materials.
- COLOR-25 changes material assignment only: all 23 modules retain identical vertices, normals and triangle faces; standard carbine remains 1,676 triangles. No new material slot, texture, geometry overlay or per-frame material allocation.
- MB_Worker01.glb contains all module meshes and ten assembly scenes. OBJ is the standard carbine assembly. These raw neutral assets have no assigned battlefield team; Unity applies team materials. Reserved equipment is not visibly stowed.
- MB_Worker01_preview.png, MB_Weapons_preview.png and MB_Paints_preview.png are reproducible renders of source geometry, **not Unity screenshots or performance evidence**. The paint sheet compares seven choices with blue front/orange rear identification panels.
- all-assemblies.mesh.json is preview input. The gameplay kit is module-kit.json under Unity Content. Existing Playable scenes reference that TextAsset and need Refresh + restart Play, not scene regeneration. Persisted generated prefabs require the Models generator in Edit Mode; GPT has not edited their YAML.
- Editor prefabs are combined static meshes. Playable assemblies split the frame into hip/knee pieces and separate the right arm/current equipment. Procedural poses preserve geometry. No full skeleton, authored animation clips, LOD or mesh collision.

[Construction contract](../../docs/UNIT_CONSTRUCTION.md), [combat rules](../../docs/COMBAT_RULES.md), [COLOR-25 handoff](../../docs/reviews/GPT_COLOR25_HANDOFF.md). Generators: MechaBattle/scripts/art/.
