using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SpeechSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// SpeechRecognitionの停止通知用タスク
        /// </summary>
        private readonly TaskCompletionSource<int> _stopRecognition = new TaskCompletionSource<int>();

        /// <summary>
        /// 音声読み上げを行うSDK
        /// </summary>
        private SpeechSynthesizer _speechSynthesizer = null;

        /// <summary>
        /// Speech Api の ApiKey
        /// </summary>
        private string _speechApiKey = string.Empty;

        /// <summary>
        /// Translator Text Api の ApiKey
        /// </summary>
        private string _translatorApiKey = string.Empty;

        /// <summary>
        ///  コンストラクタ
        /// </summary>
        public MainWindow()
        {

        }

        /// <summary>
        /// SpeechRecognizerのイベントハンドラを設定します。
        /// </summary>
        /// <param name="recognizer"></param>
        private void SetEventHandler(SpeechRecognizer recognizer)
        {
            recognizer.SessionStarted += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    japaneseTextBox.Text = "マイクに向かって喋って下さい。";
                });
            };

            recognizer.Recognizing += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    japaneseTextBox.Text = e.Result.Text;
                });
            };

            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Dispatcher.Invoke(async () =>
                    {
                        japaneseTextBox.Text = e.Result.Text;

                        // 翻訳
                        englishTextBox.Text = await Translate(e.Result.Text, "en");

                        // 音声読み上げ
                        TextToSpeech(englishTextBox.Text);
                    });

                    // 終了チェック
                    if (e.Result.Text.Contains("終わり")) _stopRecognition.TrySetResult(0);
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        japaneseTextBox.Text = "認識できませんでした。";
                    });
                }
            };

            recognizer.Canceled += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    japaneseTextBox.Text = $"ErrorCode ={ e.ErrorCode}; ErrorDetails ={ e.ErrorDetails}; ";
                });
            };

            recognizer.SessionStopped += (s, e) =>
            {
                // 終わり
            };
        }

        /// <summary>
        /// Translator Text Api で翻訳します。
        /// </summary>
        /// <param name="text">翻訳する文字列</param>
        /// <param name="lang">結果取得する言語</param>
        /// <returns></returns>
        public async ValueTask<string> Translate(string text, string lang)
        {
            string route = "/translate?api-version=3.0&to=" + lang;
            System.Object[] body = new System.Object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // In the next few sections you'll add code to construct the request.
                // Set the method to POST
                request.Method = HttpMethod.Post;

                // Construct the full URI
                request.RequestUri = new Uri("https://api.cognitive.microsofttranslator.com" + route);

                // Add the serialized JSON object to your request
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Add the authorization header
                request.Headers.Add("Ocp-Apim-Subscription-Key", _translatorApiKey);

                // Send request, get response
                var response = await client.SendAsync(request);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                dynamic jarray = JsonConvert.DeserializeObject(jsonResponse);
                foreach (var item in jarray[0]["translations"])
                {
                    if (item["to"] == lang)
                    {
                        // Found
                        return item["text"];
                    }
                }
                // Not found
                return text;
            }
        }

        /// <summary>
        /// SpeechSynthesizer を使ってテキストを読み上げます。
        /// </summary>
        /// <param name="text">読み上げるテキスト</param>
        private async void TextToSpeech(string text)
        {
            var result = await _speechSynthesizer.SpeakTextAsync(text);
            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                // スピーカーから音声が出る
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var error = new StringBuilder();
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                error.AppendLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    error.AppendLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    error.AppendLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                    error.AppendLine($"CANCELED: Did you update the subscription info?");
                }

                Dispatcher.Invoke(() => errorTextBox.Text = error.ToString());
            }
        }

        /// <summary>
        /// ウィンドウクローズの際の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // SpeechRecognition を終了させるよう待機しているスレッド動かす。
            _stopRecognition.TrySetResult(0);

            // SpeechSynthesizer を破棄する。
            _speechSynthesizer?.Dispose();
        }

        /// <summary>
        /// 開始ボタンクリック処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void startButton_Click(object sender, RoutedEventArgs e)
        {
            // APIキーを保持する
            _speechApiKey = speechApiKeyTextBox.Text;
            _translatorApiKey = translatorApiKeyTextBox.Text;

            // ボタンを無効かする
            startButton.IsEnabled = false;

            // 音声読み上げの準備
            var config1 = SpeechConfig.FromSubscription(_speechApiKey, "japaneast");
            config1.SpeechSynthesisVoiceName = "Microsoft Server Speech Text to Speech Voice (en-US, BenjaminRUS)";
            _speechSynthesizer = new SpeechSynthesizer(config1);

            // 音声認識の準備
            var config2 = SpeechConfig.FromSubscription(_speechApiKey, "japaneast");
            config2.SpeechRecognitionLanguage = "ja-jp";
            var recognizer = new SpeechRecognizer(config2);

            // 音声認識の設定
            SetEventHandler(recognizer);

            // 音声認識開始
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // 終了待機
            Task.WaitAny(_stopRecognition.Task);
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
    }
}
