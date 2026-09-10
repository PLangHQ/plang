# ImageModule

Hint: `[image]`  
Type: `PLang.Modules.ImageModule.Program`

Image generation and manipulation including QR codes, barcodes, and image processing

## Methods

### GenerateQrCode

```
GenerateQrCode(PLang.Modules.ImageModule.QrCode.QrCodeRequest request) : PLang.Modules.ImageModule.QrCode.QrCodeResult
```

Generates a QR code from data with multiple output formats

- `request` *PLang.Modules.ImageModule.QrCode.QrCodeRequest* — (see Type information in SupportingObjects)

Examples:

- generate qr code from %data% save to %filePath% => QrCodeRequest.Data=%data%, QrCodeRequest.FilePath=%filePath%
- make qr using %url%, type: url, write to %base64% => QrCodeRequest.Data=%url%, QrCodeRequest.Type="url"
- create ascii qr code from %text%, write to %ascii% => QrCodeRequest.Data=%text%, QrCodeRequest.Renderer="ascii"
- generate svg qr from %data%, write to %svg% => QrCodeRequest.Data=%data%, QrCodeRequest.Renderer="svg"
- create qr code from %text%, renderer: art, background style: circles, write to %result% => QrCodeRequest.Data=%text%, QrCodeRequest.Renderer="art", QrCodeRequest.BackgroundStyle="circles"
- generate qr for %payload%, error correction: H, dark color: #ff0000, write to %qr% => QrCodeRequest.Data=%payload%, QrCodeRequest.ErrorCorrection="H", QrCodeRequest.DarkColor="#ff0000"

