# ImageModule

Hint: `[image]`  
Type: `PLang.Modules.ImageModule.Program`

Image generation and manipulation including QR codes, barcodes, and image processing

## Methods

### ConvertImage

```
ConvertImage(String sourcePath, String targetPath, Int32 quality = 90, String format = null, String backgroundColor = null) : PLang.Modules.ImageModule.ImageResult
```

Converts an image file to another format (jpeg, png or webp) without changing its size and writes it to a new file

- `sourcePath` *String* — Path of the image file to convert
- `targetPath` *String* — Path of the file to write. Its extension decides the format unless format is given
- `quality` *Int32*, default `90` — Quality 1 to 100 for jpeg and webp
- `format` *String*, default `null` — jpeg, png or webp. Empty means the target file extension
- `backgroundColor` *String*, default `null` — Background color used to flatten transparency when the target is jpeg. Empty means white

Examples:

- convert photo.png to jpg, quality 85, write to photo.jpg => sourcePath="photo.png", targetPath="photo.jpg", quality=85
- convert %file% to webp and save as out.webp => sourcePath=%file%, targetPath="out.webp"

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

### GetImageInfo

```
GetImageInfo(String sourcePath) : PLang.Modules.ImageModule.ImageResult
```

Reads the width, height and format of an image file without changing it

- `sourcePath` *String* — Path of the image file

Examples:

- get image info of %file%, write to %info% => sourcePath=%file%

### Resize

```
Resize(String sourcePath, Int32 width, Int32 height, String targetPath, String mode = fit, Int32 quality = 90, String format = null, String backgroundColor = null, Boolean sharpen = False, Boolean center = False) : PLang.Modules.ImageModule.ImageResult
```

Resizes an image file to fit a width and height and writes the result to a new file. The mode says how the image meets the box: fit keeps the aspect ratio and fits inside the box (may enlarge), shrink-only is fit that never enlarges a smaller image, fill covers the whole box and crops the overflow, exact and stretch force the exact size and distort, pad keeps the aspect ratio and pads the rest of the box with the background color. The format is taken from the target file extension (jpg, png, webp) unless format is given

- `sourcePath` *String* — Path of the image file to resize
- `width` *Int32* — Width of the box in pixels. 0 means it follows from the height and the aspect ratio. At least one of width and height must be above 0
- `height` *Int32* — Height of the box in pixels. 0 means it follows from the width and the aspect ratio
- `targetPath` *String* — Path of the file to write. Its extension decides the format unless format is given. It may be the same as the source path
- `mode` *String*, default `fit` — fit, shrink-only, fill, exact, stretch or pad. Use shrink-only when the step says never enlarge, only shrink or do not enlarge. Use fill when it says cover, crop or fill. Use fit when nothing is said
- `quality` *Int32*, default `90` — Quality 1 to 100 for jpeg and webp. Ignored for png
- `format` *String*, default `null` — jpeg, png or webp. Empty means the target file extension
- `backgroundColor` *String*, default `null` — Background color for pad mode and for flattening transparency when the target is jpeg, for example white, black or #ff0000. Empty means white for jpeg and transparent for png and webp
- `sharpen` *Boolean*, default `False` — true when the step says sharpen
- `center` *Boolean*, default `False` — true when the step says center on a canvas of exactly width x height, so the result has the box size and the resized image sits in the middle on the background color

Examples:

- resize photo.jpg by 685x420, shrink only, quality 100, write to out.jpg => sourcePath="photo.jpg", width=685, height=420, targetPath="out.jpg", mode="shrink-only", quality=100
- resize %file% to 80x80 and save as /thumbs/%name%.jpg => sourcePath=%file%, width=80, height=80, targetPath="/thumbs/%name%.jpg"
- make a thumbnail of banner.png 300 wide, write to small.webp, quality 80 => sourcePath="banner.png", width=300, height=0, targetPath="small.webp", quality=80
- resize cover.jpg to 500x500, fill, sharpen, write to square.jpg => sourcePath="cover.jpg", width=500, height=500, targetPath="square.jpg", mode="fill", sharpen=true
- resize logo.png by 400x400, pad with white background, write to padded.jpg => sourcePath="logo.png", width=400, height=400, targetPath="padded.jpg", mode="pad", backgroundColor="white"
- breyta stærð á mynd.jpg í 230x400, aldrei stækka, vista sem lítil.jpg => sourcePath="mynd.jpg", width=230, height=400, targetPath="lítil.jpg", mode="shrink-only"

