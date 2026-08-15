# Dataset workspace

公開資料集下載後放在 `raw/`。原始影像不納入版本控制。

目前選定資料集：Kolektor Surface-Defect Dataset 2（KSDD2）

- 官方頁面：https://www.vicos.si/resources/kolektorsdd2/
- 用途：工業表面 OK／NG 判定與缺陷定位
- 授權：CC BY-NC-SA 4.0
- 組成：約 2,979 張正常影像與 356 張瑕疵影像
- ZIP 大小：853,126,555 bytes
- SHA-256：`EDCDB486809B24F1D17B785E30C52FAFC5999554DD5FE18DDF77B61CEB6F36A8`

可重現下載：

```powershell
.\scripts\Get-KSDD2.ps1
```
