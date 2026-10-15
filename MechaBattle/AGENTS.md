# Unity 実装用の追加指示

../AGENTS.md と ../MechaBattle_MasterPlan/AGENTS.md を適用する。
Assets/Game/ が実装先。純粋C#アセンブリは noEngineReferences を維持する。
共有型は Contracts に置く。Scene / Prefab は Editor API で生成する。
新規ファイルは Unity が生成する .meta と合わせて管理し、既存GUIDを再生成しない。
