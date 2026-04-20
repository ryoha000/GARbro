# garbro-cli

GARbro のコマンドラインインターフェース (`garbro-cli.exe`) です。
ビジュアルノベル用アーカイブリソースの識別・一覧表示・抽出・変換をスクリプトや外部ツールから実行できます。
全ての結果は **JSON** 形式で標準出力に出力されます。

---

## ビルド方法

Visual Studio 2022 で `GARbro.sln` を開き、`GARbro.Cli` プロジェクトをビルドしてください。  
コマンドラインからビルドする場合は以下を実行します (Developer Command Prompt を使用):

```bat
nuget restore GARbro.sln
msbuild Cli\GARbro.Cli.csproj /p:Configuration=Release
```

ビルド成功後、`bin\Release\garbro-cli.exe` に実行ファイルが生成されます。

---

## 基本的な使い方

```
garbro-cli <コマンド> [オプション]
```

### 共通オプション

| オプション | 説明 |
|---|---|
| `--input <パス>` | 入力ファイル・アーカイブのパス (ほぼ全コマンドで必須) |
| `--json` | JSON 形式で出力する (デフォルトでも JSON が出力されます) |
| `--pretty` | JSON を整形して出力する |
| `--hints <ゲームタイトル>` | 暗号化アーカイブ解読に使うゲームタイトルのヒント |

---

## コマンド一覧

| コマンド | 説明 |
|---|---|
| [`identify`](#identify) | ファイルの形式を識別する |
| [`probe`](#probe) | アーカイブ/ファイルの概要を素早く確認する |
| [`list`](#list) | アーカイブ内のエントリ一覧を取得する |
| [`extract`](#extract) | アーカイブからファイルを抽出する |
| [`convert`](#convert) | 画像・音声ファイルを変換する |
| [`formats`](#formats) | 対応フォーマット一覧を取得する |

---

## identify

ファイル (またはディレクトリ) の形式を識別します。

```
garbro-cli identify --input <パス> [--hints <ゲームタイトル>] [--pretty]
```

### 成功時レスポンス例

```json
{
  "ok": true,
  "command": "identify",
  "input": "C:\\game\\data.arc",
  "kind": "archive",
  "format": {
    "tag": "ARC",
    "description": "SomeEngine resource archive"
  },
  "engine": "SomeEngine",
  "is_supported": true,
  "requires_additional_context": false,
  "notes": []
}
```

### 暗号化アーカイブの場合

`requires_additional_context: true` が返される場合は `--hints` にゲームタイトルを指定してください。

```
garbro-cli identify --input data.arc --hints "ゲームタイトル"
```

---

## probe

アーカイブまたはファイルの概要 (エントリ数・種別分布・サンプルファイル名) を素早く確認します。  
次に実行すべきコマンドも `recommended_next_actions` として提示されます。

```
garbro-cli probe --input <パス> [--hints <ゲームタイトル>] [--pretty]
```

### レスポンス例 (アーカイブ)

```json
{
  "ok": true,
  "command": "probe",
  "input": "C:\\game\\data.arc",
  "kind": "archive",
  "format": { "tag": "ARC", "description": "..." },
  "entry_count": 512,
  "top_types": [
    { "type": "image", "count": 300 },
    { "type": "script", "count": 100 },
    { "type": "audio", "count":  112 }
  ],
  "samples": ["bg001.grp", "ev001.grp", "op.ogg"],
  "recommended_next_actions": [
    "list --input \"C:\\game\\data.arc\" --limit 100 --offset 0",
    "list --input \"C:\\game\\data.arc\" --type script --limit 100 --offset 0",
    "extract --input \"C:\\game\\data.arc\" --type script --out <dir>"
  ]
}
```

---

## list

アーカイブ内のエントリ一覧を取得します。ページネーション・フィルタリングに対応しています。

```
garbro-cli list --input <アーカイブ> [オプション] [--pretty]
```

### オプション

| オプション | デフォルト | 説明 |
|---|---|---|
| `--limit <N>` | 100 | 取得するエントリ数の上限 |
| `--offset <N>` | 0 | スキップするエントリ数 |
| `--type <種別>` | (なし) | `image` / `audio` / `script` / `archive` などで絞り込み |
| `--filter <文字列>` | (なし) | ファイル名に含まれる文字列 or 拡張子 (`*.grp`) で絞り込み |

### レスポンス例

```json
{
  "ok": true,
  "command": "list",
  "input": "C:\\game\\data.arc",
  "container": {
    "kind": "archive",
    "format": { "tag": "ARC", "description": "..." }
  },
  "entries": [
    { "path": "bg001.grp", "name": "bg001.grp", "type": "image", "size": 204800, "packed_size": 204800 }
  ],
  "pagination": {
    "limit": 100, "offset": 0, "count": 1, "total_matching": 1
  },
  "summary": { "entry_count": 1, "total_matching": 1 }
}
```

### 使用例

```bat
:: スクリプトファイルのみ一覧表示
garbro-cli list --input data.arc --type script --pretty

:: 拡張子フィルタ + ページネーション
garbro-cli list --input data.arc --filter "*.grp" --limit 50 --offset 50
```

---

## extract

アーカイブからファイルを抽出します。

```
garbro-cli extract --input <アーカイブ> --out <出力ディレクトリ> [オプション] [--pretty]
```

### オプション

| オプション | デフォルト | 説明 |
|---|---|---|
| `--out <ディレクトリ>` | (必須) | 出力先ディレクトリ |
| `--entry <エントリ名>` | (なし) | 抽出する特定エントリ (複数回指定可) |
| `--entry-file <テキストファイル>` | (なし) | 抽出するエントリ名リストが書かれたテキストファイル (1行1エントリ) |
| `--type <種別>` | (なし) | 種別で絞り込んで抽出 |
| `--filter <文字列>` | (なし) | ファイル名フィルタ |
| `--overwrite <ポリシー>` | `skip` | `skip` / `overwrite` / `rename` のいずれか |
| `--flatten` | false | ディレクトリ構造をフラットにして抽出 (ファイル名のみ) |
| `--dry-run` | false | 実際には書き込まずに結果を確認するドライラン |
| `--safe-root <パス>` | `--out` と同じ | パストラバーサル防止のためのルートパス |

### レスポンス例

```json
{
  "ok": true,
  "command": "extract",
  "input": "C:\\game\\data.arc",
  "output_dir": "C:\\out",
  "results": [
    { "entry": "bg001.grp", "size": 204800, "status": "extracted", "output_path": "C:\\out\\bg001.grp" },
    { "entry": "bg002.grp", "size": 204800, "status": "skipped_existing" }
  ],
  "summary": { "requested": 2, "succeeded": 1, "failed": 0 }
}
```

### エントリのステータス

| ステータス | 説明 |
|---|---|
| `extracted` | 正常に抽出された |
| `skipped_existing` | ファイルが既存で `--overwrite skip` のためスキップ |
| `dry-run` | ドライランのため未書き込み |
| `failed` | エントリ個別のエラー |

### 使用例

```bat
:: 全エントリを抽出
garbro-cli extract --input data.arc --out C:\out

:: スクリプトのみ抽出
garbro-cli extract --input data.arc --out C:\scripts --type script

:: 特定エントリだけ抽出
garbro-cli extract --input data.arc --out C:\out --entry bg001.grp --entry bg002.grp

:: ドライランで確認
garbro-cli extract --input data.arc --out C:\out --dry-run --pretty
```

---

## convert

ゲーム独自形式の画像・音声ファイルを一般的な形式に変換します。  
- 画像 → **PNG**
- 音声 → **WAV**

単体ファイルの変換と、アーカイブ内エントリの変換の両方に対応しています。

```
garbro-cli convert --input <ファイル or アーカイブ> --to <png|wav> --out <出力ディレクトリ> [オプション] [--pretty]
```

### オプション

| オプション | 説明 |
|---|---|
| `--to <形式>` | (必須) `png` または `wav` |
| `--out <ディレクトリ>` | (必須) 出力先ディレクトリ |
| `--entry <エントリ名>` | アーカイブ内の特定エントリを変換 |
| `--overwrite <ポリシー>` | `skip` / `overwrite` / `rename` (デフォルト: `skip`) |
| `--safe-root <パス>` | パストラバーサル防止のためのルートパス |

### レスポンス例

```json
{
  "ok": true,
  "command": "convert",
  "input": "C:\\game\\bg001.grp",
  "target_format": "png",
  "output_dir": "C:\\out",
  "result": {
    "status": "converted",
    "output_path": "C:\\out\\bg001.png",
    "media_type": "image"
  }
}
```

### 使用例

```bat
:: 単体画像ファイルを PNG に変換
garbro-cli convert --input bg001.grp --to png --out C:\out

:: アーカイブ内のエントリを変換
garbro-cli convert --input data.arc --entry op.ogg --to wav --out C:\out
```

---

## formats

GARbro が対応しているフォーマットの一覧を取得します。

```
garbro-cli formats [--pretty]
```

### レスポンス例

```json
{
  "ok": true,
  "command": "formats",
  "formats": [
    { "tag": "ARC",  "kind": "archive", "description": "SomeEngine resource archive" },
    { "tag": "PNG",  "kind": "image",   "description": "Portable Network Graphics" },
    { "tag": "OGG",  "kind": "audio",   "description": "Ogg Vorbis audio" }
  ]
}
```

---

## エラーレスポンス

エラー時は `ok: false` が返され、`error` フィールドにコードとメッセージが含まれます。

```json
{
  "ok": false,
  "command": "extract",
  "input": "C:\\game\\data.arc",
  "error": {
    "code": "INPUT_NOT_SUPPORTED",
    "message": "Unsupported format: ..."
  }
}
```

### エラーコード一覧

| コード | 説明 |
|---|---|
| `INVALID_ARGUMENT` | 必須引数が不足、または値が不正 |
| `INPUT_NOT_FOUND` | 指定したファイル・パスが存在しない |
| `INPUT_NOT_SUPPORTED` | 未対応の形式 |
| `ARCHIVE_OPEN_FAILED` | アーカイブを開けなかった |
| `ENTRY_NOT_FOUND` | 指定したエントリがアーカイブ内に存在しない |
| `EXTRACTION_FAILED` | 抽出処理が失敗した |
| `CONVERSION_FAILED` | 変換処理が失敗した |
| `OUTSIDE_SAFE_ROOT` | 出力パスが `--safe-root` の範囲外 |
| `REQUIRES_ADDITIONAL_CONTEXT` | アーカイブの解読に追加情報 (ゲームタイトル等) が必要 |
| `ACCESS_DENIED` | アクセス権限がない |
| `INTERNAL_ERROR` | 予期しない内部エラー |

---

## 典型的なワークフロー例

```bat
:: 1. アーカイブの概要確認
garbro-cli probe --input data.arc --pretty

:: 2. スクリプトファイルの一覧確認
garbro-cli list --input data.arc --type script --pretty

:: 3. スクリプトを全て抽出
garbro-cli extract --input data.arc --type script --out C:\scripts

:: 4. 背景画像を PNG に一括変換
garbro-cli extract --input data.arc --type image --out C:\images
::    ※ ゲーム独自形式の場合は convert コマンドを個別に使用してください
```
