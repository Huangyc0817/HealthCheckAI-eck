# 🏥 HealthCheckAI 智能健檢報告系統

這是一個使用 C# 與 ASP.NET MVC 開發的醫療資訊平台（大學畢業專題）。
本系統旨在協助醫師快速生成健檢報告的 AI 智能摘要、自動風險分級，並提供來賓（病患）友善的報告查詢介面，包含多語系翻譯與 Azure 語音播報功能，降低醫療數據的閱讀門檻。

## 🚀 本機開發與安裝指南

如果同學想在自己的電腦上運行此專案，請務必按照以下步驟進行設定，否則專案將無法成功啟動或連線至資料庫。

### 步驟 1：下載專案與還原設定檔
1. 將本專案 Clone 或下載 ZIP 到您的電腦中並解壓縮。
2. 進入專案根目錄，找到 `appsettings.example.json` 檔案。
3. 將該檔案重新命名為 **`appsettings.json`**（這是為了保護真實金鑰不上傳而做的安全機制）。

### 步驟 2：設定資料庫連線與 API 金鑰
請使用 Visual Studio 打開剛改名好的 `appsettings.json`，並修改以下兩個關鍵數值：
1. **資料庫連線 (Connection String)**：
   找到 `"DefaultConnection"`，將 `Server=` 後方的伺服器名稱，修改為您自己電腦中 SQL Server Management Studio (SSMS) 或 LocalDB 的伺服器名稱（例如：`localhost`、`.\SQLEXPRESS` 或 `(localdb)\MSSQLLocalDB`）。
2. **Azure 語音服務金鑰 (Azure API Key)**：
   找到相關的 API 設定欄位，填入本專案專屬的 Azure API Key（若無金鑰請向專題組員索取）。

### 步驟 3：自動生成本機資料庫 (Entity Framework Core)
因為資料庫不會隨程式碼上傳，您必須讓系統在您的電腦中自動建表：
1. 用 Visual Studio 打開本專案的解決方案 (`HealthCheckAI.sln`)。
2. 在上方選單點選 **「工具」** ➔ **「NuGet 套件管理員」** ➔ **「套件管理器主控台 (Package Manager Console)」**。
3. 在下方彈出的終端機輸入框中，輸入以下指令並按下 Enter：
   ```powershell
   Update-Database
