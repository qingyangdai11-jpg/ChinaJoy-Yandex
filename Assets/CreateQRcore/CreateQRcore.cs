using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

public class CreateQRcore : MonoBehaviour
{

    [Tooltip("二维码的尺寸")]
    public Vector2 qrSize = new Vector2(256, 256);

    public  string QrCodeStrSolo = "https://www.baidu.com/";

    //在屏幕上显示二维码  
    public RawImage RawImage_QRcore;

    //public ErrorCorrectionLevel ErrorCorrectionLevel = ErrorCorrectionLevel.Q;
    public bool TestCreate=true;

    public static CreateQRcore Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (TestCreate)
        {
            if (RawImage_QRcore != null)
            {
                Create(QrCodeStrSolo);
            }
            else
            {
                Debug.LogWarning("[CreateQRcore] RawImage_QRcore is null. Skipping TestCreate.");
            }
        }
    }

    /// <summary>
    /// 定义方法生成二维码 
    /// </summary>
    /// <param name="textForEncoding">需要生产二维码的字符串</param>
    /// <param name="width">宽</param>
    /// <param name="height">高</param>
    /// <returns></returns>       
    private static Color32[] Encode(string textForEncoding, int width, int height)
    {
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = height,
                Width = width
            }
        };
        return writer.Write(textForEncoding);
    }

    [Tooltip("要比实际的url链接长度要长")]
    public int NormalizeUrlInt = 120;
    // 标准化链接的函数
    private string NormalizeURL(string url)
    {
        // 在这里可以对链接进行标准化处理，确保它们的长度相同
        // 这里只是一个简单的示例，你可以根据实际情况进行更复杂的处理
        return url.PadRight(NormalizeUrlInt, ' ');
    }

    public void Create(string QrCodeStrSolo, bool NeedNormalizeURL=false)
    {
        if (QrCodeStrSolo.Length > 1)
        {
            if (NeedNormalizeURL)
            {
                QrCodeStrSolo = NormalizeURL(QrCodeStrSolo);
            }
            
            if (RawImage_QRcore != null)
            {
                //二维码写入图片    
                BitMatrix bitMatrix = CreatQRcodeToBit(QrCodeStrSolo);
                //生成的二维码图片附给RawImage    
                RawImage_QRcore.texture = LoadQRTex(bitMatrix);
            }
            else
            {
                Debug.LogError("[CreateQRcore] RawImage_QRcore is null! Cannot display QR code.");
            }
        }
    }

    /// <summary>  
    /// 生成二维码  
    /// </summary>  
    public void Create(string QrCodeStrSolo ,RawImage image_qrCode, bool NeedNormalizeURL = false)
    {
      
        if (QrCodeStrSolo.Length > 1)
        {
            if (NeedNormalizeURL)
            {
                QrCodeStrSolo = NormalizeURL(QrCodeStrSolo);

            }
            //二维码写入图片    

            BitMatrix bitMatrix= CreatQRcodeToBit(QrCodeStrSolo);

            //生成的二维码图片附给RawImage    
            image_qrCode.texture = LoadQRTex(bitMatrix);
        }
    }

    public void CreateTransparentQRcore(string text) {

        RawImage_QRcore.texture = GenerateTransparentQRcore(text);
    }
    public void CreateTransparentQRcore(string text, RawImage rawImage)
    {

        rawImage.texture = GenerateTransparentQRcore(text);
    }

    public void CreateTransparentQRcoreColor(string text, RawImage rawImage,Color? color = null)
    {
        Color finalColor = color ?? Color.black;

        rawImage.texture = GenerateTransparentQRcore(text, finalColor);
    }
    //透明的二维码
    public Texture2D GenerateTransparentQRcore(string text)
    {
        var qrCodeWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = 256,
                Width = 256,
                Margin = 1
            }
        };

        // 生成像素数据（byte数组，每4个字节表示一个RGBA像素）
        var pixelData = qrCodeWriter.Write(text);

        // 创建透明纹理
        Texture2D qrTexture = new Texture2D(pixelData.Width, pixelData.Height, TextureFormat.RGBA32, false);
        Color32[] colors = new Color32[pixelData.Width * pixelData.Height];

        // 遍历所有像素（每4个byte表示一个像素）
        for (int pixelIndex = 0; pixelIndex < pixelData.Pixels.Length; pixelIndex += 4)
        {
            int x = (pixelIndex / 4) % pixelData.Width;
            int y = (pixelIndex / 4) / pixelData.Width;

            // 判断是否为二维码模块（黑色部分）
            bool isQRModule =
                pixelData.Pixels[pixelIndex] == 0   // R
                && pixelData.Pixels[pixelIndex + 1] == 0 // G 
                && pixelData.Pixels[pixelIndex + 2] == 0; // B

            colors[y * pixelData.Width + x] = isQRModule
                ? new Color32(0, 0, 0, 255)    // 二维码模块（黑色）
                : new Color32(0, 0, 0, 0);      // 透明背景
        }

        qrTexture.SetPixels32(colors);
        qrTexture.Apply();
        return qrTexture;
    }

    public Texture2D GenerateTransparentQRcore(string text, Color? color = null)
    {
        //  default(Color)
        // 默认值为黑色（可能不是你想要的）

        Color finalColor = color ?? Color.black;
        Color32 color32 = finalColor;

        var qrCodeWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = 256,
                Width = 256,
                Margin = 1
            }
        };

        // 生成像素数据（byte数组，每4个字节表示一个RGBA像素）
        var pixelData = qrCodeWriter.Write(text);

        // 创建透明纹理
        Texture2D qrTexture = new Texture2D(pixelData.Width, pixelData.Height, TextureFormat.RGBA32, false);
        Color32[] colors = new Color32[pixelData.Width * pixelData.Height];

        // 遍历所有像素（每4个byte表示一个像素）
        for (int pixelIndex = 0; pixelIndex < pixelData.Pixels.Length; pixelIndex += 4)
        {
            int x = (pixelIndex / 4) % pixelData.Width;
            int y = (pixelIndex / 4) / pixelData.Width;

            // 判断是否为二维码模块（黑色部分）
            bool isQRModule =
                pixelData.Pixels[pixelIndex] == 0   // R
                && pixelData.Pixels[pixelIndex + 1] == 0 // G 
                && pixelData.Pixels[pixelIndex + 2] == 0; // B

            colors[y * pixelData.Width + x] = isQRModule
                ? new Color32(color32.r, color32.g, color32.b, color32.a)    // 二维码模块（黑色）
                : new Color32(0, 0, 0, 0);      // 透明背景
        }

        qrTexture.SetPixels32(colors);
        qrTexture.Apply();
        return qrTexture;
    }
    #region 返回二维码数据
    /// <summary>
    /// 将返回的url转换成zxing使用的bit类型
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private BitMatrix CreatQRcodeToBit(string url)
    {
        Dictionary<EncodeHintType, object> hints = new Dictionary<EncodeHintType, object>();
        //设置编码方式  
        hints.Add(EncodeHintType.CHARACTER_SET, "UTF-8");
        //设置识别精度，设为高精度，可以在二维码上叠加Logo
        hints.Add(EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.M);
        //设置边框厚度，默认为4
        hints.Add(EncodeHintType.MARGIN, 1);

        //hints.Add(EncodeHintType.MIN_SIZE, 256);
        //hints.Add(EncodeHintType.MAX_SIZE, 256);



        return new MultiFormatWriter().encode(url, BarcodeFormat.QR_CODE, (int)qrSize.x, (int)qrSize.y, hints);
    }

    #endregion


    #region 加载二维码图片
    /// <summary>
    /// 加载二维码图片
    /// </summary>
    /// <param name="bit"></param>
    /// <returns></returns>
    private Texture2D LoadQRTex(BitMatrix bit)
    {
        //设置二维码大小  
        Texture2D encoded = new Texture2D(256, 256);
        int width = bit.Width;
        int height = bit.Height;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (bit[x, y])
                {
                    encoded.SetPixel(y, x, Color.black);
                    #region
                    /*彩色二维码
                    if (x < width / 2 && y < height / 2)
                    {
                        encoded.SetPixel(y, x, Color.blue);
                    }
                    else if (x < width / 2 && y > height / 2)
                    {
                        encoded.SetPixel(y, x, Color.yellow);
                    }
                    else if (x > width / 2 && y > height / 2)
                    {
                        encoded.SetPixel(y, x, Color.green);
                    }
                    else
                    {
                        encoded.SetPixel(y, x, Color.red);
                    }
                    */
                    #endregion
                }
                else
                {
                    encoded.SetPixel(y, x, Color.white);
                }
            }
        }
        encoded.Apply();
        //Debug.Log("二维码已经生成");
        return encoded;
    }
    #endregion
}