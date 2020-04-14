using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

using Windows.Data.Pdf;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace HttpClientSample
{
    /// <summary>
    /// HTTP通信・PDF変換サンプル
    /// <see cref="https://qiita.com/rawr/items/f78a3830d894042f891b"/>
    /// <see cref="https://water2litter.net/rum/post/cs_pdf_wpf/"/>
    /// </summary>
    public partial class Form1 : Form
    {
        // Nifty Mobile Backend - File store
        //private const string REST_API_DOWNLOAD_URL = @"https://mbaas.api.nifcloud.com/2013-09-01/applications/KFZmOFF9ZXWbdfQS/publicFiles/test.pdf";
        private const string REST_API_DOWNLOAD_URL = @"http://192.168.56.101/download.php";
        //private const string REST_API_UPLOAD_URL_BASE = @"https://mbaas.api.nifcloud.com/2013-09-01/files/";
        private const string REST_API_UPLOAD_URL_BASE = @"http://192.168.56.101/upload.php";

        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// MBAASからPDFをダウンロードしてPictureBoxに画像として表示する。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonDownload_Click(object sender, EventArgs e)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                // HTTPリクエスト作成
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, REST_API_DOWNLOAD_URL);

                // HTTPヘッダの設定(必要に応じて)
                //httpRequestMessage.Headers.Add(@"key", value);

                // Basic認証(必要に応じて)
                //string username = @"username";
                //string password = @"password";
                //string authorizationParam = Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", username, password)));
                //httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationParam);

                Console.WriteLine("<HTTP request Header>-----------------------------------------------------------");
                Console.WriteLine(httpRequestMessage.Headers.ToString());
                Console.WriteLine("--------------------------------------------------------------------------------");

                // HTTPリクエスト送信(非同期)
                HttpResponseMessage httpResponseMessage = null;
                try
                {
                    httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                }
                catch (Exception exception)
                {
                    // 通信エラー
                    string message = String.Format("通信エラー : {0}", exception.Message);
                    Console.WriteLine(exception.StackTrace);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }
                // HTTPステータスチェック
                if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                {
                    // 成功
                    Console.WriteLine("ダウンロード成功");
                }
                else
                {
                    // 失敗
                    string message = String.Format("ダウンロードエラー : {0}", httpResponseMessage.ReasonPhrase);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }

                Console.WriteLine("<HTTP request Header>-----------------------------------------------------------");
                Console.WriteLine(httpResponseMessage.Headers.ToString());
                Console.WriteLine("--------------------------------------------------------------------------------");

                try
                {
                    // HTTPレスポンスからPDFデータを取得
                    using (Stream pdfStream = await httpResponseMessage.Content.ReadAsStreamAsync())
                    {
                        // .NETストリームをWindowsランタイムストリームに変換
                        IInputStream inputStream = WindowsRuntimeStreamExtensions.AsInputStream(pdfStream);

                        // ストリームからPDFオブジェクト作成
                        PdfDocument pdfDocument = await PdfDocument.LoadFromStreamAsync((IRandomAccessStream)inputStream);

                        // PDFの１ページ目をストリームにレンダリング
                        InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
                        using (PdfPage pdfPage = pdfDocument.GetPage(0))
                        {
                            await pdfPage.RenderToStreamAsync(inMemoryRandomAccessStream);
                        }

                        // ストリームからビットマップオブジェクト作成
                        Bitmap bitmap = new Bitmap(inMemoryRandomAccessStream.AsStreamForRead());

                        // PixtureBoxにビットマップを描画
                        pictureBox.Width = bitmap.Width;
                        pictureBox.Height = bitmap.Height;
                        pictureBox.Image = bitmap;
                    }
                }
                catch (Exception exception)
                {
                    // PDF変換失敗
                    string message = String.Format("PDFの変換に失敗 : {0}", exception.Message);
                    Console.WriteLine(exception.StackTrace);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                }
            }
        }

        /// <summary>
        /// 選択した画像ファイルをPDFに変換してMBAASにアップロードする。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ButtonUpload_Click(object sender, EventArgs e)
        {
            // PNG -> PDF 変換(ImageMagick呼び出し)
            string fromPng = @"D:\VisualStudio\HttpClientSample\kotoko.png";
            string toJpeg = @"D:\VisualStudio\HttpClientSample\kotoko.jpeg";
            string toPDF = @"D:\VisualStudio\HttpClientSample\kotoko.pdf";

            Process convertProcess = null;
            try
            {
                // TODO : PNGから直接PDFを作成するとApacheがcontent-typeを削除する？？
                // TODO : 回避策として一旦JPEGに変換してからPDFに変換する
                // TODO : 課題：2回変換するので時間がかかる

                // プロセス情報設定(PNG -> JPEG)
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = @"D:\work\ImageMagick-7.0.10-Q16\convert.exe";
                processStartInfo.Arguments = fromPng + " " + toJpeg;
                /* コマンドプロンプトを開かないようにする設定 */
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;

                // プロセス実行
                convertProcess = Process.Start(processStartInfo);
                convertProcess.WaitForExit();

                // 戻り値チェック
                if (convertProcess.ExitCode != 0)
                {
                    // PDF変換失敗
                    string message = String.Format("PDF変換エラー(PNG -> JPEG) : {0}", convertProcess.ExitCode);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }

                // プロセス情報設定(JPEG -> PDF)
                processStartInfo.Arguments = toJpeg + " " + toPDF;

                // プロセス実行
                convertProcess = Process.Start(processStartInfo);
                convertProcess.WaitForExit();

                // 戻り値チェック
                if (convertProcess.ExitCode != 0)
                {
                    // PDF変換失敗
                    string message = String.Format("PDF変換エラー(JPEG -> PDF) : {0}", convertProcess.ExitCode);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }
            }
            catch (Exception exception)
            {
                // PDF変換失敗
                string message = String.Format("PDF変換エラー : {0}", exception.Message);
                Console.WriteLine(message);
                MessageBox.Show(message);
                return;
            }
            finally
            {
                if (convertProcess != null)
                {
                    convertProcess.Dispose();
                }
            }
            // HTTP通信
            using (HttpClient httpClient = new HttpClient())
            {
                // HTTPリクエスト作成
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, REST_API_UPLOAD_URL_BASE);

                // Basic認証(必要に応じて)
                //string username = @"username";
                //string password = @"password";
                //string authorizationParam = Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", username, password)));
                //httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationParam);

                // HTTPリクエストボディ作成
                /* Multipart ContentにPDFを設定 */
                MultipartFormDataContent content = new MultipartFormDataContent();
                FileStream fileStream = File.OpenRead(toPDF);
                StreamContent streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    // TODO : この辺はサーバの仕様に合わせる
                    Name = "s_file",
                    FileName = Path.GetFileName(toPDF)
                };
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                content.Add(streamContent);
                httpRequestMessage.Content = content;

                Console.WriteLine("<HTTP request Header>-----------------------------------------------------------");
                Console.WriteLine(httpRequestMessage.Headers.ToString());
                Console.WriteLine("--------------------------------------------------------------------------------");

                // HTTPリクエスト送信(非同期)
                HttpResponseMessage httpResponseMessage = null;

                try
                {
                    httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                }
                catch (Exception exception)
                {
                    // 通信エラー
                    string message = String.Format("通信エラー : {0}", exception.Message);
                    Console.WriteLine(exception.StackTrace);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }
                finally
                {
                    streamContent.Dispose();
                    fileStream.Dispose();

                    // 中間ファイルを削除
                    if (File.Exists(toPDF))
                    {
                        File.Delete(toPDF);
                    }
                    if (File.Exists(toJpeg))
                    {
                        File.Delete(toJpeg);
                    }
                }
                // HTTPステータスチェック
                if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                {
                    // 成功
                    Console.WriteLine("アップロード成功");
                }
                else
                {
                    // 失敗
                    string message = String.Format("アップロードエラー : {0}", httpResponseMessage.ReasonPhrase);
                    Console.WriteLine(message);
                    MessageBox.Show(message);
                    return;
                }

                Console.WriteLine("<HTTP request Header>-----------------------------------------------------------");
                Console.WriteLine(httpResponseMessage.Headers.ToString());
                Console.WriteLine("--------------------------------------------------------------------------------");
            }
        }
    }
}
