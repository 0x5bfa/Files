# Files.Core / Files.App 実装状況

`Files.Core` はUI非依存のstorage、capability、operation、browse session、application modelを提供します。
`Files.App` は既存WinUIを維持したまま、primary Windows folder browseをCoreへ接続済みです。

## Files.Core

| 領域 | 状況 |
| --- | --- |
| application model | application、window、tab、1..2 pane、pane history、preview、browse sessionを実装済み |
| Windows storage | resolve、enumeration、stable reference、property、thumbnail、change sourceを実装済み |
| FTP storage | FTP/FTPS source、stream、property、operationを実装済み |
| browse | context ownership、atomic navigation/refresh、incremental reconciliation、selection、projectionを実装済み |
| operation | create、rename、copy、move、deleteとcollision policyを実装済み |
| preview | stream previewとWindows Shell Preview Handler sessionを実装済み |
| threading | ordered/concurrent/operation用message-pumped Shell STAを実装済み |
| validation | Files.Core unit/integration testとbenchmark projectが存在 |

## Files.Appで接続済み

- 起動時に `FilesCoreRuntime` を1つ作成し、最終終了時に非同期破棄する。
- 既存tabごとに `TabModel` lease、split paneごとに `PaneModel` を割り当てる。
- rooted local Windows folderをCoreでresolve、enumerate、watch、refreshする。
- Coreのversion付き一覧を既存 `ListedItem` collectionへUI thread上で投影する。
- selectionとviewportをCoreへ送り、stable keyでselectionをreconcileする。
- property/thumbnail prefetchを既存詳細/grid表示へ反映する。
- renameをCoreのstorage operation pipelineへ送り、通知欠落時だけrefreshする。
- HomeのQuick Access、drive、network、recent enumerationをCore Windows sourceへ委譲する。
- file tags/Start pinning用の旧 `IStorageService` shapeをCore Windows sourceへ委譲する。

## 互換経路

次は機能を失わないために既存Files.App経路を維持しています。

- Home、Search、Library、Tag、FTPの画面navigationとitem presentation
- Frame back/forward、tab session persistence、toolbar command state
- delete、copy、move、create、clipboard、drag/drop、context menu、sharing
- preview paneのWinUI hostと既存preview routing
- Recycle Bin watcher、drive monitoring、taskbar progress、object picker
- 旧WinRT FTP itemと一時的な資格情報cache

互換adapterの一覧と破棄順序は[Files.AppのCore統合アーキテクチャ](files-app.md)に記載しています。

## 次の優先順位

1. toolbarのback/forward/up/refreshを `PaneModel` へroutingし、Frame履歴との二重管理を解消する。
2. delete/copy/move/createをCore operation requestへ移し、既存dialog、進行状況、elevation、server継続をadapter化する。
3. preview UIをCore `PaneModel.Preview` とWindows Shell preview sessionへ接続する。
4. Search/Library/Tag/FTPを型付き `BrowseLocation` とCore sourceへ移す。
5. `ShellViewModel` のCore投影を独立したtestable presentation modelへ抽出する。

Files.Coreの完了はFiles.Appの全機能移行完了を意味しません。現在の完了境界は、ローカルWindows folderの
主要表示フローがCoreを正として動き、既存UIが狭いadapterでそれを描画できる段階です。
