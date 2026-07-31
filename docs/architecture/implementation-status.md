# Files.Core / Files.App2 / Files.App 実装状況

`Files.Core` はUI非依存のstorage、capability、operation、browse session、application modelを提供します。
`Files.App2` は既存のサービスグラフを持ち込まない新しいWinUIホストとして、primary Windows folder browseをCoreへ接続しています。
`Files.App` は移行期間の互換経路として残します。

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

## Files.App2で接続済み

- `MainWindow`から`RootView`を起点に、custom `TabView`、window単位の`NavigationToolbar`、native `NavigationView`、
  `ToolbarView`、`PaneHost`、`PaneView`、`PaneContentView`、`FolderBrowser`、`DetailsFolderView`を独立したcontrolとして構成する。
- custom `TabView`はWinUI 3 title bar APIを使い、`PaneHost`は`Panes` collectionをItemsRepeaterへ投影する。
- `DetailsFolderView`は現在stable key selectionをCoreへroutingするListView実装で、表示モードの差し替え境界を提供する。
- 起動時に `FilesCoreRuntime` を1つ作成し、最終終了時に非同期破棄する。
- `FilesApplicationModel` が作成した `WindowModel` の active `TabModel`/`PaneModel` をUI adapterへ渡す。
- Home と rooted local Windows folderをCoreでresolve、enumerate、watch、refreshする。
- Coreのversion付き一覧をDispatcherQueue上でApp2のpresentation collectionへ投影する。
- selectionをstable keyでCoreへ送り、Coreのselection stateをUIへreconcileする。
- back/forward/up、path navigation、refreshを `PaneModel` へroutingする。
- `App2CommandRegistration`でstable command IDをprocess-level registryへ登録し、window単位の
  `WindowCommandManager`からnavigation、tab、pane、Home、folder double-clickを実行する。

## 旧 Files.App の互換経路

次は機能を失わないために既存Files.App経路を維持しています。

- Home、Search、Library、Tag、FTPの画面navigationとitem presentation
- Frame back/forward、tab session persistence、toolbar command state
- delete、copy、move、create、clipboard、drag/drop、context menu、sharing
- preview paneのWinUI hostと既存preview routing
- Recycle Bin watcher、drive monitoring、taskbar progress、object picker
- 旧WinRT FTP itemと一時的な資格情報cache

互換adapterの一覧と破棄順序は[Files.AppのCore統合アーキテクチャ](files-app.md)に記載しています。これは新しいApp2の依存方向を
規定する文書ではありません。

## 次の優先順位

1. Details viewをList/Grid/Card/Columnsへ拡張し、view settingsとviewport reportingを接続する。
2. App2へ preview UIをCore `PaneModel.Preview` とWindows Shell preview sessionへ接続する。
3. delete/copy/move/createをCore operation requestへ移し、既存dialog、進行状況、elevation、server継続をadapter化する。
4. Search/Library/Tag/FTPを型付き `BrowseLocation` とCore sourceへ移す。
5. App2のWinUI presentation model、localization、activation、永続化を追加し、旧 Files.App の互換経路を機能単位で削除する。

Files.Coreの完了はFiles.AppまたはFiles.App2の全機能移行完了を意味しません。現在の完了境界は、ローカルWindows folderの
主要表示フローがCoreを正として動き、App2が狭いadapterでそれを描画できる段階です。
