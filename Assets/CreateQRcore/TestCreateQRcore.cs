using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestCreateQRcore : MonoBehaviour
{

    public string QrCodeStrSolo = "https://www.baidu.com/";

    public RawImage rawImageWhiteBackGroundQRcore;
    public RawImage rawImageTransparentQRcore;
    public RawImage rawImageTransparentQRcoreDIY;

    public Color color_diy;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Chick_CreateWhiteBackGroundQRcore() {

        CreateQRcore.Instance.Create(QrCodeStrSolo, rawImageWhiteBackGroundQRcore);
    }

    public void Chick_CreateTransparentQRcore()
    {
        CreateQRcore.Instance.CreateTransparentQRcore(QrCodeStrSolo, rawImageTransparentQRcore);

    }

    public void Chick_CreateTransparentQRcoreDIY()
    {

        CreateQRcore.Instance.CreateTransparentQRcoreColor(QrCodeStrSolo, rawImageTransparentQRcoreDIY,color_diy);
    }
}
