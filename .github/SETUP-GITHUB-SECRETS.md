# Hướng Dẫn Setup CI/CD Hoàn Chỉnh — CarRentalSystem

Tài liệu này hướng dẫn bạn thiết lập toàn bộ CI/CD pipeline từ đầu đến cuối.
**Thứ tự:** Làm theo đúng 8 bước dưới đây.

---

## Tổ Quan — CI/CD Pipeline Cần Gì

```
GitHub Actions
├── security-scan          ← Quét lỗ hổng bảo mật (Trivy + OWASP)
├── build-and-test         ← Build, test, SonarCloud, Docker push
├── cypress-e2e            ← Test trình duyệt tự động
├── deploy-staging         ← Deploy lên môi trường staging
└── deploy-production      ← Deploy lên production
```

Mỗi job cần credentials và config riêng. Ta sẽ thiết lập lần lượt.

---

## Bước 1 — SonarCloud (Phân Tích Code Chất Lượng)

### SonarCloud là gì?

Dịch vụ phân tích code tự động: tìm bugs, code smells, lỗ hổng bảo mật,
đo độ phủ test, và chấm điểm chất lượng code từ 0→A.

### 1a. Tạo tài khoản SonarCloud

1. Mở trình duyệt → truy cập [sonarcloud.io](https://sonarcloud.io)
2. Click **Log in with GitHub** (nhanh nhất)
3. Cấp quyền cho SonarCloud truy cập repo GitHub của bạn
4. Sau khi đăng nhập, bạn sẽ thấy giao diện SonarCloud

### 1b. Tạo Organization (Tổ chức)

SonarCloud cần một Organization để nhóm các project.

1. Trên SonarCloud → click **+** ở góc trên phải → **Create new organization**
2. Chọn **Use your GitHub account**
3. Chọn **GitHub organization** của bạn
4. Plan: chọn **Free** (miễn phí cho public repo, hoặc free cho private repo nếu
   tài khoản Student/OSS)
5. **Organization key** — đây là giá trị bạn cần ghi lại

> **Organization Key** là gì? Là slug duy nhất của tổ chức, ví dụ: `huynhloi`,
> `carrentalteam`. Bạn cần nhập đúng key này vào GitHub Variable `SONAR_ORG`.

### 1c. Import repository CarRentalSystem

1. Trong Organization vừa tạo → click **Import repository**
2. Tìm và chọn repo `CarRentalSystem`
3. Click **Import**

### 1d. Lấy SONAR_TOKEN

1. Trên SonarCloud → click **avatar** (góc trên phải) → **My Account**
2. Vào tab **Security**
3. Ô **Generate Token** → nhập tên: `CarRentalSystem-CI`
4. Click **Generate**
5. **COPY TOKEN NGAY** — token chỉ hiện **một lần duy nhất**

```
SONAR_TOKEN có dạng: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

> Lưu token này ở đâu đó tạm (Notepad), sẽ dùng ở Bước 6.

### 1e. Ghi lại thông tin SonarCloud

Mở Notepad, ghi lại 2 giá trị:

```
SONAR_ORG: <organization key bạn vừa tạo, ví dụ: huynhloi>
SONAR_TOKEN: <token vừa copy>
```

---

## Bước 2 — GitHub Container Registry (Lưu Docker Images)

### ghcr.io là gì?

Registry miễn phí của GitHub để lưu Docker images. Mỗi khi pipeline chạy,
Docker image sẽ được push lên đây. Server production sẽ pull về để chạy.

### 2a. Bật Packages trên GitHub

1. Mở GitHub repo → **Settings** (tab repo, không phải account settings)
2. Menu bên trái → cuộn xuống **Access** (trong mục Packages)
3. Đặt thành **Public** (nếu repo public) hoặc **Private** (nếu repo private)
4. Click **Save**

### 2b. Tạo GitHub Personal Access Token (PAT)

PAT là "mật khẩu" cho pipeline để push images lên ghcr.io.

1. Trên GitHub → click **avatar** → **Settings**
2. Menu bên trái → **Developer settings** (cuối trang)
3. **Personal access tokens** → **Tokens (classic)**
4. Click **Generate new token** → **Generate new token (classic)**
5. Điền:
   - **Name**: `carrental-docker-access`
   - **Expiration**: chọn **90 days** (hoặc custom → 90 ngày)
   - **Scopes**: tick vào **`write:packages`**
6. Click **Generate token**
7. **COPY TOKEN NGAY** — không hiện lại lần hai

```
REGISTRY_PASSWORD: ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
REGISTRY_USERNAME: <username GitHub của bạn, ví dụ: huynhloi>
REGISTRY_HOST: ghcr.io
```

> **Lưu ý quan trọng:** Token này có expiry date. Khi hết hạn, pipeline sẽ
> fail với lỗi "unauthorized". Bạn cần tạo token mới và cập nhật secret trước
> khi hết hạn. Khuyến nghị set reminder 1 tuần trước expiry.

---

## Bước 3 — Azure (Host Ứng Dụng)

### Azure App Service là gì?

Nền tảng web server của Microsoft Azure. Thay vì tự cài server, Azure lo
toàn bộ: load balancing, SSL, scaling, monitoring. Bạn chỉ cần deploy code lên.

### 3a. Đăng nhập Azure

1. Mở trình duyệt → [portal.azure.com](https://portal.azure.com)
2. Đăng nhập tài khoản Microsoft (có thể dùng email Outlook/Hotmail)

> Nếu chưa có tài khoản Azure → đăng ký **Free Account** (được $200 credit
> dùng thử 30 ngày). Đủ để học và dev không tốn phí.

### 3b. Tạo Resource Group

Resource Group = "thư mục" chứa toàn bộ resources của 1 môi trường.

**Tạo Staging Resource Group:**

1. Trên Azure Portal → search **"Resource groups"**
2. Click **Create**
3. Subscription: chọn subscription của bạn
4. Resource group: `carrental-rg-staging`
5. Region: **Southeast Asia** (Singapore — gần Việt Nam, latency thấp)
6. Click **Review + create** → **Create**

**Tạo Production Resource Group:**

1. Làm tương tự
2. Tên: `carrental-rg-prod`
3. Region: **Southeast Asia**

### 3c. Tạo Azure App Service Plan

App Service Plan = "gói server" quyết định CPU, RAM, giá tiền.

1. Search **"App Service plans"** → **Create**
2. Subscription: chọn subscription
3. Resource Group: `carrental-rg-staging`
4. Name: `carrental-staging-plan`
5. Operating System: **Linux** (vì dùng .NET on Linux container)
6. Region: **Southeast Asia**
7. Pricing tier: click **Change size**
   - Chọn **B1** (Basic) — $13/tháng, đủ cho dev/staging
   - Hoặc **F1** (Free) — miễn phí, nhưng app ngủ sau 20 phút không dùng
8. Click **Apply** → **Review + create** → **Create**

**Production plan** tương tự: `carrental-prod-plan`, pricing tier **B1** hoặc **S1**.

### 3d. Tạo App Service (Web App)

**Staging App Service:**

1. Search **"App Services"** → **Create** → **Web App**
2. Tab **Basics**:
   - Subscription: chọn subscription
   - Resource Group: `carrental-rg-staging`
   - Name: `carrental-staging` (tên này là subdomain: `carrental-staging.azurewebsites.net`)
   - Runtime: **.NET 10 (LTS)**
   - Region: **Southeast Asia**
   - App Service Plan: chọn `carrental-staging-plan` đã tạo
3. Tab **Monitoring**:
   - Enable Application Insights: **No** (bỏ qua để tiết kiệm)
4. Tab **Review + create** → **Create**
5. Đợi ~2-3 phút → Deploy complete

**Production App Service:**

1. Làm tương tự
2. Resource Group: `carrental-rg-prod`
3. Name: `carrental-prod`
4. Plan: `carrental-prod-plan`

> **Ghi lại URLs:**
> - Staging: `https://carrental-staging.azurewebsites.net`
> - Production: `https://carrental-prod.azurewebsites.net`

### 3e. Tạo Azure SQL Database

Database = nơi lưu toàn bộ dữ liệu ứng dụng (users, bookings, cars...).

**Staging Database:**

1. Search **"Azure SQL"** → **Create** → **Single database**
2. Tab **Basics**:
   - Resource Group: `carrental-rg-staging`
   - Database name: `carrental-staging-db`
   - Server: click **Create new**
     - Server name: `carrental-staging-sql` (unique toàn cầu)
     - Server admin login: `sqladmin`
     - Password: `CarRental@Staging123!` (nhớ password này!)
     - Location: Southeast Asia
   - Want to use SQL elastic pool: **No**
3. Tab **Networking**:
   - Connectivity method: **Public endpoint**
   - Allow Azure services: **Yes** (để GitHub Actions kết nối được)
   - Add current client IP: **Yes**
4. Tab **Security** → bỏ **Microsoft Defender for SQL** (tiết kiệm)
5. Tab **Review + create** → **Create**
6. Đợi ~5 phút

**Production Database:**

1. Làm tương tự
2. Resource Group: `carrental-rg-prod`
3. Database name: `carrental-prod-db`
4. Server name: `carrental-prod-sql`
5. Password: `CarRental@Prod!@#$5678`

### 3f. Lấy Connection Strings

Sau khi database tạo xong:

1. Vào **SQL database** → `carrental-staging-db`
2. Menu trái → **Connection strings**
3. Copy chuỗi **ADO.NET**, thay password:

```
Staging:
Server=tcp:carrental-staging-sql.database.windows.net,1433;
Database=carrental-staging-db;
User Id=sqladmin;
Password=CarRental@Staging123!;
Encrypt=True;TrustServerCertificate=False;
MultipleActiveResultSets=true

Production:
Server=tcp:carrental-prod-sql.database.windows.net,1433;
Database=carrental-prod-db;
User Id=sqladmin;
Password=CarRental@Prod!@#$5678;
Encrypt=True;TrustServerCertificate=False;
MultipleActiveResultSets=true
```

> ** Quan trọng:** Password phải thỏa policy của Azure SQL:
> - Tối thiểu 8 ký tự
> - Có chữ hoa, chữ thường, số, ký tự đặc biệt

### 3g. Lấy Subscription ID

1. Trên Azure Portal → click **Search** → nhập **"Subscriptions"**
2. Mở **Subscriptions**
3. Copy **Subscription ID** (dạng UUID: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)

---

## Bước 4 — Azure Service Principal (Cho Phép GitHub Deploy)

### Service Principal (SPN) là gì?

"Robot account" cho phép GitHub Actions đại diện bạn thao tác trên Azure
mà không cần đăng nhập tay. Mỗi môi trường nên có SPN riêng.

### 4a. Cài đặt Azure CLI

Nếu chưa có Azure CLI trên máy:

1. Windows: tải installer từ [docs.microsoft.com/azure/cli/install-azure-cli-windows](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-windows)
   Hoặc chạy trong PowerShell:
   ```powershell
   Invoke-WebRequest -Uri https://aka.ms/installazurecliwindowsx64 -OutFile .\AzureCLI.msi
   Start-Process msiexec.exe -Wait -ArgumentList '/i AzureCLI.msi /quiet'
   rm .\AzureCLI.msi
   ```
2. Linux/Mac: `curl -sL https://aka.ms/InstallAzureCLILinux | bash`

### 4b. Đăng nhập Azure CLI

```bash
az login
```

Trình duyệt sẽ mở → đăng nhập Azure → CLI nhận credentials.

### 4c. Set subscription đang dùng

```bash
az account set --subscription "YOUR_SUBSCRIPTION_ID"
az account show --output table
```

Xác nhận subscription đúng trước khi tạo SPN.

### 4d. Tạo Service Principal cho Staging

```bash
az ad sp create-for-rbac \
  --name "carrental-staging-sp" \
  --role "Contributor" \
  --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/carrental-rg-staging" \
  --output json
```

Output trả về JSON:

```json
{
  "appId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "password": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "tenant": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

**Ghi lại** `appId`, `password`, `tenant` → dùng ở Bước 6.

### 4e. Tạo Service Principal cho Production

```bash
az ad sp create-for-rbac \
  --name "carrental-prod-sp" \
  --role "Contributor" \
  --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/carrental-rg-prod" \
  --output json
```

Ghi lại output tương tự.

### 4f. Tóm tắt giá trị Azure CLI

Mở Notepad, ghi lại đầy đủ:

```
SUBSCRIPTION_ID: <subscription ID từ Bước 3g>

STAGING_SPN:
  appId: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  password: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
  tenant: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

PROD_SPN:
  appId: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  password: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
  tenant: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

---

## Bước 5 — GitHub Variables

### Biến (Variables) là gì?

Giá trị công khai, không nhạy cảm, dùng chung cho tất cả pipeline runs.
Không cần giấu, nhưng để một chỗ quản lý thay vì hard-code trong workflow.

### Cách thêm Variables

1. Mở **GitHub repo** → **Settings**
2. Menu trái → **Secrets and variables** → **Actions**
3. Tab **Variables** → **New repository variable**

Thêm **từng dòng** theo bảng:

| # | Variable Name | Value |
|---|---|---|
| 1 | `REGISTRY_HOST` | `ghcr.io` |
| 2 | `SONAR_ORG` | `<organization key từ Bước 1b>` |
| 3 | `STAGING_APP_NAME` | `carrental-staging` |
| 4 | `STAGING_RESOURCE_GROUP` | `carrental-rg-staging` |
| 5 | `STAGING_APP_URL` | `https://carrental-staging.azurewebsites.net` |
| 6 | `PRODUCTION_APP_NAME` | `carrental-prod` |
| 7 | `PRODUCTION_RESOURCE_GROUP` | `carrental-rg-prod` |
| 8 | `PRODUCTION_APP_URL` | `https://carrental-prod.azurewebsites.net` |
| 9 | `AZURE_DEPLOYMENT_DOMAIN` | `azurewebsites.net` |

Sau khi thêm xong 9 biến, giao diện sẽ như thế này:

```
┌──────────────────────────────────────────────────────┐
│  Actions variables                         [+ Add]   │
├──────────────────────────────────────────────────────┤
│  REGISTRY_HOST                    ghcr.io      [...] │
│  SONAR_ORG                        huynhloi     [...] │
│  STAGING_APP_NAME                 carrental-staging [...] │
│  STAGING_RESOURCE_GROUP           carrental-rg-staging [...] │
│  STAGING_APP_URL                  https://carrental-staging... [...] │
│  PRODUCTION_APP_NAME              carrental-prod [...] │
│  PRODUCTION_RESOURCE_GROUP        carrental-rg-prod [...] │
│  PRODUCTION_APP_URL               https://carrental-prod... [...] │
│  AZURE_DEPLOYMENT_DOMAIN          azurewebsites.net [...] │
└──────────────────────────────────────────────────────┘
```

---

## Bước 6 — GitHub Secrets

### Secrets là gì?

Dữ liệu nhạy cảm — mật khẩu, token, private key. GitHub mã hóa và **không
hiện giá trị** sau khi lưu. Không bao giờ commit secrets vào code.

### 6a. Thêm Secret: SONAR_TOKEN

1. GitHub repo → **Settings** → **Secrets and variables** → **Actions**
2. Tab **Secrets** → **New repository secret**
3. Name: `SONAR_TOKEN`
4. Secret: `<token từ Bước 1d>`
5. Click **Add secret**

### 6b. Thêm Secret: REGISTRY_USERNAME

1. **New repository secret**
2. Name: `REGISTRY_USERNAME`
3. Secret: `<username GitHub của bạn>`
4. **Add secret**

### 6c. Thêm Secret: REGISTRY_PASSWORD

1. **New repository secret**
2. Name: `REGISTRY_PASSWORD`
3. Secret: `<GitHub PAT từ Bước 2b>` (bắt đầu bằng `ghp_`)
4. **Add secret**

### 6d. Thêm Secret: AZURE_STAGING_CREDENTIALS

Lấy giá trị JSON từ Bước 4d:

```json
{
  "appId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "password": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "tenant": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Copy **toàn bộ JSON** (không thay đổi gì) vào Secret value.

1. Name: `AZURE_STAGING_CREDENTIALS`
2. Secret: `<paste JSON>`
3. **Add secret**

### 6e. Thêm Secret: AZURE_PROD_CREDENTIALS

Tương tự Bước 6d, dùng JSON từ Bước 4e.

1. Name: `AZURE_PROD_CREDENTIALS`
2. Secret: `<paste JSON>`
3. **Add secret**

### 6f. Thêm Secret: AZURE_SUBSCRIPTION_ID

1. Name: `AZURE_SUBSCRIPTION_ID`
2. Secret: `<subscription ID từ Bước 3g>`
3. **Add secret**

### 6g. Thêm Secret: STAGING_CONNECTION_STRING

1. Name: `STAGING_CONNECTION_STRING`
2. Secret: `<chuỗi kết nối từ Bước 3f>`
3. **Add secret**

### 6h. Thêm Secret: PRODUCTION_CONNECTION_STRING

1. Name: `PRODUCTION_CONNECTION_STRING`
2. Secret: `<chuỗi kết nối từ Bước 3f>`
3. **Add secret**

Sau khi xong, tab Secrets sẽ có **8 secrets** (chỉ hiện tên, không hiện giá trị):

```
┌──────────────────────────────────────────────────────┐
│  Actions secrets                       [+ New secret] │
├──────────────────────────────────────────────────────┤
│  AZURE_PROD_CREDENTIALS                 ••••••••••• │
│  AZURE_STAGING_CREDENTIALS             ••••••••••• │
│  AZURE_SUBSCRIPTION_ID                 ••••••••••• │
│  PRODUCTION_CONNECTION_STRING           ••••••••••• │
│  REGISTRY_PASSWORD                     ••••••••••• │
│  REGISTRY_USERNAME                     ••••••••••• │
│  SONAR_TOKEN                           ••••••••••• │
│  STAGING_CONNECTION_STRING             ••••••••••• │
└──────────────────────────────────────────────────────┘
```

---

## Bước 7 — GitHub Environments

### Environments là gì?

Tính năng GitHub để phân chia môi trường, cấu hình protection rules,
và theo dõi deployment history riêng biệt cho mỗi môi trường.

### 7a. Tạo Environment `staging`

1. GitHub repo → **Settings** → **Environments** (menu trái)
2. Click **New environment**
3. Name: `staging`
4. **Deployment branches**:
   - Chọn **Selected branches**
   - Click **Add deployment branch rule**
   - Branch name pattern: `develop`
   - Tick ✅ **Required reviewers** (để trống — ai cũng deploy được)
   - Click **Add rule**
5. Click **Configure environment**

### 7b. Tạo Environment `production`

1. Click **New environment**
2. Name: `production`
3. **Deployment branches**:
   - Chọn **Selected branches**
   - Click **Add deployment branch rule**
   - Branch name pattern: `main`
   - Tick ✅ **Required reviewers**
   - Reviewers: click **Select reviewers** → thêm chính bạn
   - ✅ **Wait timer**: `0` minutes
   - ✅ **Deployment branch rule**: chỉ cho phép `main`
   - Click **Add rule**
4. Click **Configure environment**

> **Tại sao production cần reviewer?** Để ngăn deploy tự động khi có ai đó
> push thẳng lên main. Mỗi lần deploy production, GitHub sẽ gửi notification
> yêu cầu bạn approve trước. Đây là lớp bảo vệ cuối cùng.

Sau khi xong, Environments sẽ hiển thị:

```
┌─────────────────────────────────────────────────────────┐
│  Environments                                    [+ New] │
├──────────────────────────────────────────────────────────┤
│  staging   ● 0 deployments   Configured                 │
│  production ● 0 deployments  Requires 1 reviewer        │
└─────────────────────────────────────────────────────────┘
```

---

## Bước 8 — Bật Environments + Test Pipeline

### 8a. Uncomment environment blocks trong workflow

Mở file `.github/workflows/ci-cd-tests.yml`:

**Tìm dòng gần job `deploy-staging`** (khoảng dòng 497):

```yaml
    # ── Tạo Environment "staging" trên GitHub trước khi uncomment ──
    # Settings → Environments → New environment → Name: "staging"
    # environment:
    #   name: staging
    #   url: ${{ vars.STAGING_APP_URL }}
```

Bỏ comment (xóa `# `) để thành:

```yaml
    environment:
      name: staging
      url: ${{ vars.STAGING_APP_URL }}
```

**Tương tự với job `deploy-production`** (khoảng dòng 605):

Bỏ comment:

```yaml
    environment:
      name: production
      url: ${{ vars.PRODUCTION_APP_URL }}
```

### 8b. Commit & Push

```bash
git add .github/workflows/ci-cd-tests.yml
git commit -m "feat: enable GitHub environments in CI/CD pipeline"
git push origin develop
```

### 8c. Theo dõi pipeline chạy

1. Mở GitHub repo → tab **Actions**
2. Click vào workflow run mới nhất
3. Quan sát các jobs chạy:

```
[1] security-scan          ← ~2-3 phút
[2] build-and-test         ← ~5-8 phút  (chạy song song với [1])
[3] cypress-e2e            ← ~10-15 phút (chạy sau [2])
[4] deploy-staging         ← ~3-5 phút   (chỉ khi push lên develop)
```

4. Nếu job **deploy-staging** yêu cầu approve → vào **Environments** →
   `production` → click **Review deployments** → **Approve and deploy**

### 8d. Kiểm tra kết quả

- **Staging**: truy cập `https://carrental-staging.azurewebsites.net`
- **SonarCloud**: vào [sonarcloud.io](https://sonarcloud.io) → project CarRentalSystem
- **Docker images**: GitHub repo → **Packages** → tìm `carrental`
- **Security**: GitHub repo → **Security** → **Vulnerability alerts**

---

## Checklist Cuối Cùng

### Bước 1 — SonarCloud ✅

- [ ] Đăng ký SonarCloud.io bằng GitHub OAuth
- [ ] Tạo Organization
- [ ] Import repo CarRentalSystem
- [ ] Tạo SONAR_TOKEN → lưu vào Notepad
- [ ] Ghi lại SONAR_ORG

### Bước 2 — GitHub Container Registry ✅

- [ ] Bật Packages access (Settings → Packages → Access)
- [ ] Tạo GitHub PAT với quyền `write:packages`
- [ ] Ghi lại REGISTRY_USERNAME, REGISTRY_PASSWORD

### Bước 3 — Azure ✅

- [ ] Đăng ký Azure Free Account
- [ ] Tạo `carrental-rg-staging` và `carrental-rg-prod`
- [ ] Tạo App Service Plans (B1)
- [ ] Tạo App Services (`carrental-staging`, `carrental-prod`)
- [ ] Tạo SQL Databases
- [ ] Lấy Connection Strings
- [ ] Lấy Subscription ID

### Bước 4 — Azure SPN ✅

- [ ] Cài Azure CLI
- [ ] `az login`
- [ ] Tạo SPN cho staging → ghi lại appId, password, tenant
- [ ] Tạo SPN cho production → ghi lại appId, password, tenant

### Bước 5 — GitHub Variables ✅

- [ ] Thêm 9 Variables vào GitHub

### Bước 6 — GitHub Secrets ✅

- [ ] SONAR_TOKEN
- [ ] REGISTRY_USERNAME
- [ ] REGISTRY_PASSWORD
- [ ] AZURE_STAGING_CREDENTIALS (JSON)
- [ ] AZURE_PROD_CREDENTIALS (JSON)
- [ ] AZURE_SUBSCRIPTION_ID
- [ ] STAGING_CONNECTION_STRING
- [ ] PRODUCTION_CONNECTION_STRING

### Bước 7 — GitHub Environments ✅

- [ ] Tạo environment `staging` (chỉ branch `develop`)
- [ ] Tạo environment `production` (chỉ branch `main` + reviewer required)

### Bước 8 — Test ✅

- [ ] Uncomment 2 environment blocks trong workflow
- [ ] Push lên develop
- [ ] Vào Actions tab theo dõi pipeline
- [ ] Kiểm tra staging URL hoạt động

---

## Xử Lý Lỗi Thường Gặp

### Lỗi: `unauthorized: authentication required` (Docker push)

**Nguyên nhân:** REGISTRY_PASSWORD (GitHub PAT) hết hạn hoặc thiếu quyền.

**Fix:**
1. GitHub → Settings → Developer settings → Personal access tokens
2. Tìm token `carrental-docker-access`
3. Nếu hết hạn → **Regenerate** token mới
4. GitHub repo → Settings → Secrets and variables → Actions → Secrets
5. Cập nhật `REGISTRY_PASSWORD` bằng token mới

### Lỗi: `Resource not found` (Azure deployment)

**Nguyên nhân:** Tên App Service hoặc Resource Group không khớp với Azure.

**Fix:** Kiểm tra chính xác tên trên Azure Portal → so sánh với GitHub Variables.

### Lỗi: `No webhook configured` (SonarCloud)

**Nguyên nhân:** Chưa cấu hình webhook để GitHub Actions báo kết quả về SonarCloud.

**Fix:**
1. SonarCloud → Administration → Configuration → Webhooks
2. Click **Create**
3. URL: `https://sonarcloud.io/api/github/hooks/organization?organization=<SONAR_ORG>`
4. Chọn **Security token** → tạo token mới trên SonarCloud
5. Pipeline tự động gửi kết quả qua SonarScanner, không cần webhook cho CI/CD

### Lỗi: `AZURE_STAGING_CREDENTIALS` invalid format

**Nguyên nhân:** JSON bị thay đổi thứ tự key hoặc có ký tự thừa.

**Fix:** Đảm bảo paste đúng JSON thuần, không có dấu ngoặc kép ngoài:

```
✅ {"appId":"...","password":"...","tenant":"..."}
❌ '{"appId":"...", "password":"...", "tenant":"..."}'
❌ "{\"appId\":\"...\"}"
```

### Lỗi: Database migration fail

**Nguyên nhân:** Firewall Azure SQL chặn IP GitHub Actions runner.

**Fix:**
1. Vào Azure SQL Server → **Firewalls and virtual networks**
2. Đặt **Allow Azure services and resources to access this server** = **Yes**
3. Click **Save**

---

## Chi Phí Ước Tính (Tháng)

| Service | Tier | Chi phí/tháng |
|---|---|---|
| Azure App Service (Staging) | B1 | ~$13 |
| Azure App Service (Production) | B1 | ~$13 |
| Azure SQL Database (Staging) | Serverless | ~$5-10 |
| Azure SQL Database (Production) | Basic 5 DTU | ~$5-10 |
| SonarCloud | Free | $0 |
| GitHub Container Registry | Free (500MB) | $0 |
| **Tổng ước tính** | | **~$36-46/tháng** |

> Nếu dùng Azure Free Account → được $200 credit/tháng, dùng miễn phí.

---

## Cập Nhật Định Kỳ

| Hạng mục | Tần suất | Cách cập nhật |
|---|---|---|
| GitHub PAT (REGISTRY_PASSWORD) | 90 ngày | Tạo token mới → cập nhật secret |
| Azure SPN passwords | 1 năm | Azure Portal → Azure Active Directory → App registrations → Certificates & secrets → New client secret |
| NuGet packages | Mỗi PR | CI tự cập nhật (Trivy/OWASP quét mỗi lần chạy) |
| SonarCloud rules | Tự động | Cập nhật theo phiên bản SonarScanner |

---

## Backup & Khôi Phục

### Nếu mất GitHub Secrets

Tất cả đều có thể tạo lại:
- `SONAR_TOKEN` → SonarCloud → My Account → Regenerate
- `REGISTRY_PASSWORD` → GitHub → Developer settings → Regenerate PAT
- `AZURE_*_CREDENTIALS` → Chạy lại `az ad sp create-for-rbac` → tạo SPN mới
- Connection strings → Azure Portal → SQL databases → Connection strings

### Nếu mất Azure resources

Xóa resource group = xóa toàn bộ. Để tránh mất chi phí:
- Staging: có thể xóa khi không dùng, tạo lại nhanh bằng ARM template
- Production: luôn giữ, backup database hàng ngày

---

## Các Tài Liệu Tham Khảo

- GitHub Actions: https://docs.github.com/en/actions
- Azure App Service: https://learn.microsoft.com/azure/app-service/
- SonarCloud: https://sonarcloud.io/documentation
- Azure CLI: https://docs.microsoft.com/cli/azure/reference-index
- Docker + GitHub Container Registry: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry
