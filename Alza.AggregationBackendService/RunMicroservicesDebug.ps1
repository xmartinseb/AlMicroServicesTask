cd ..\Alza.ProductService
Start-Process powershell -ArgumentList "-NoExit", "-File RunDebug.ps1"

cd ..\Alza.PricingService
Start-Process powershell -ArgumentList "-NoExit", "-File ..\Alza.PricingService\RunDebug.ps1"

cd ..\Alza.StockService
Start-Process powershell -ArgumentList "-NoExit", "-File ..\Alza.StockService\RunDebug.ps1"

cd ..\Alza.AggregationBackendService