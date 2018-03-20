using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DalSoft.RestClient;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunctionApp3
{
    public static class mix
    {
        [FunctionName("mix")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            name = name ?? data?.name;
            MakeRequest(name);
            return name == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
                : req.CreateResponse(HttpStatusCode.OK, "hello");
        }
        static string hubname = "demo0122hub";
        static string connectstr = "Endpoint=sb://demo0122.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YlskpUE1XI7PewlD0XK61k+mqrUTmkORKdhYbF8D3Ms=";
        static async void MakeRequest(string name)
        {
            var client = new HttpClient();
            //var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "91a230665f834be5969776abd16e91ac");

            // Request parameters
            //queryString["returnFaceId"] = "true";
            //queryString["returnFaceLandmarks"] = "false";
            //queryString["returnFaceAttributes"] = "emotion";//
            //var uri = "https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect?" + queryString;
            var uri = "https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=emotion";
            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{\"url\":\""+ name +"\"}");
            //byte[] byteData = Encoding.UTF8.GetBytes("{\"url\":\"https://www.emotivebrand.com/wp-content/uploads/2014/09/20160425153327-emotions-mosaic-different-expressions-faces-angry-mad-sad-smile-feelings.jpg\"}");

            using (var content = new ByteArrayContent(byteData))
            {
                //連接到 FACE API
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");//設定回傳格式為JSON
                response = await client.PostAsync(uri, content);//儲存回傳資訊
                string datareply = await response.Content.ReadAsStringAsync();//把response的content部分拿出來
                //MessageBox.Show("" + datareply);
                emotion emo = new emotion();//紀錄情緒數值
                parameters par = new parameters();//記錄每張臉的座標
                JArray rss = JArray.Parse(datareply);//把content轉成Jarray
                float EmoStat = 0;//總體情緒狀態
                float CurrentEmoStat = 0;
                JArray jObj = (JArray)JsonConvert.DeserializeObject(datareply);//把string轉回成object以算tiple數
                int PNum = jObj.Count;//計算總人數
                var parlist = new List<parameters>();
                int ParMeanX = 0;
                int ParMeanY = 0;
                for (int person = 0; person < PNum; person++)
                {


                    //紀錄每個情緒數值並給予權重
                    emo.anger = (float)rss[person]["faceAttributes"]["emotion"]["anger"] * -1;
                    emo.contempt = (float)rss[person]["faceAttributes"]["emotion"]["contempt"] * -1;
                    emo.disgust = (float)rss[person]["faceAttributes"]["emotion"]["disgust"] * -1;
                    emo.fear = (float)rss[person]["faceAttributes"]["emotion"]["fear"] * -1;
                    emo.happiness = (float)rss[person]["faceAttributes"]["emotion"]["happiness"];
                    emo.neutral = (float)rss[person]["faceAttributes"]["emotion"]["neutral"];
                    emo.sadness = (float)rss[person]["faceAttributes"]["emotion"]["sadness"] * -1;
                    emo.surprise = (float)rss[person]["faceAttributes"]["emotion"]["surprise"];
                    //加總情緒數值
                    CurrentEmoStat = emo.anger + emo.contempt + emo.disgust + emo.fear + emo.happiness + emo.neutral + emo.sadness + emo.surprise;
                    EmoStat += CurrentEmoStat;

                    parlist.Add(new parameters() { x = (int)rss[person]["faceRectangle"]["left"], y = (int)rss[person]["faceRectangle"]["top"], emo = CurrentEmoStat });
                    ParMeanX += parlist[person].x;
                    ParMeanY += parlist[person].y;
                }
                if (PNum != 0)
                {
                    ParMeanX = ParMeanX / PNum;
                    ParMeanY = ParMeanY / PNum;

                    float TopLeftEmo = 0;
                    int TopLeftCount = 0;

                    float TopRightEmo = 0;
                    int TopRightCount = 0;

                    float BotLeftEmo = 0;
                    int BotLeftCount = 0;

                    float BotRightEmo = 0;
                    int BotRightCount = 0;

                    for (int i = 0; i < PNum; i++)
                    {
                        if (parlist[i].x > ParMeanX && parlist[i].y > ParMeanY)
                        {
                            TopLeftEmo += parlist[i].emo;
                            TopLeftCount++;
                        }
                        else if (parlist[i].x <= ParMeanX && parlist[i].y > ParMeanY)
                        {
                            TopRightEmo += parlist[i].emo;
                            TopRightCount++;
                        }
                        else if (parlist[i].x > ParMeanX && parlist[i].y <= ParMeanY)
                        {
                            BotLeftEmo += parlist[i].emo;
                            BotLeftCount++;
                        }
                        else
                        {
                            BotRightEmo += parlist[i].emo;
                            BotRightCount++;
                        }
                    }
                    TopLeftEmo = TopLeftEmo / (float)TopLeftCount;
                    TopRightEmo = TopRightEmo / (float)TopRightCount;
                    BotLeftEmo = BotLeftEmo / (float)BotLeftCount;
                    BotRightEmo = BotRightEmo / (float)BotRightCount;
                    EmoStat = EmoStat / (float)PNum;//計算情緒平均值
                    string value = "{" + "\"totalScore\":" + "\"" + EmoStat + "\"," + "\"tlemoScore\": " + "\"" + TopLeftEmo + "\"," + "\"tremoScore\": " + "\"" + TopRightEmo + "\"," + "\"blemoScore\": " + "\"" + BotLeftEmo + "\"," + "\"bremoScore\": " + "\"" + BotRightEmo + "\"" + "}";

                    var eventhubclient = EventHubClient.CreateFromConnectionString(connectstr, hubname);
                    try
                    {

                        //var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.powerbi.com/beta/84c31ca0-ac3b-4eae-ad11-519d80233e6f/datasets/64061bfd-0861-4e4f-ae4f-960f90c57130/rows?key=Z3w1Nz0%2BFrcm04XD84TiQfox%2BfUjaE84p%2BzMmM4ZE0HgdRw%2BlowRijATYTCCXDLFJTObf0e7ovBeSA3TcOMkpQ%3D%3D");
                        //httpWebRequest.ContentType = "application/json";
                        //httpWebRequest.Method = "POST";

                        //using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                        //{
                        //    string json = "{" + "\"emo1\": " + "\"" + TopLeftEmo + "\"," + "\"emo2\": " + "\"" + TopRightEmo + "\"," + "\"emo3\": " + "\"" + BotLeftEmo + "\"," + "\"emo4\": " + "\"" + BotRightEmo + "\"" + "}";
                        //    streamWriter.Write(json);
                        //    streamWriter.Flush();
                        //    streamWriter.Close();
                        //}
                        //var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                        //using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                        //{
                        //    var result = streamReader.ReadToEnd();
                        //}


                        var message = "{\"emo1\":" + TopLeftEmo + ",\"emo2\":" + TopRightEmo + ",\"emo3\":" + BotLeftEmo + ",\"emo4\":" + BotRightEmo + ",\"totalemo\":" + EmoStat + ",\"max\":1" + ",\"min\":-1,\"Gate\":0" + "}";
                        //Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, message);
                        eventhubclient.Send(new EventData(Encoding.UTF8.GetBytes(message)));
                        //return LH;
                    }
                    catch (Exception ex)
                    {
                        //return "   ";
                    }

                    //return emotionJson;
                }
                else
                {

                    //var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.powerbi.com/beta/84c31ca0-ac3b-4eae-ad11-519d80233e6f/datasets/64061bfd-0861-4e4f-ae4f-960f90c57130/rows?key=Z3w1Nz0%2BFrcm04XD84TiQfox%2BfUjaE84p%2BzMmM4ZE0HgdRw%2BlowRijATYTCCXDLFJTObf0e7ovBeSA3TcOMkpQ%3D%3D");
                    //httpWebRequest.ContentType = "application/json";
                    //httpWebRequest.Method = "POST";

                    //using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    //{
                    //    string json = "{" + "\"emo1\": " + "\"" + null + "\"," + "\"emo2\": " + "\"" + null + "\"," + "\"emo3\": " + "\"" + null + "\"," + "\"emo4\": " + "\"" + null + "\"" + "}";
                    //    streamWriter.Write(json);
                    //    streamWriter.Flush();
                    //    streamWriter.Close();
                    //}
                    //var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    //using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    //{
                    //    var result = streamReader.ReadToEnd();
                    //}


                    string value = "{" + "\"totalScore\":" + "\"" + null + "\"," + "\"tlemoScore\": " + "\"" + null + "\"," + "\"tremoScore\": " + "\"" + null + "\"," + "\"blemoScore\": " + "\"" + null + "\"," + "\"bremoScore\": " + "\"" + null + "\"" + "}";
                    float TopLeftEmo = 0;
                    float TopRightEmo = 0;
                    float BotLeftEmo = 0;
                    float BotRightEmo = 0;
                    var eventhubclient = EventHubClient.CreateFromConnectionString(connectstr, hubname);
                    try
                    {
                        //EmotionScore emotionScore = JsonConvert.DeserializeObject<EmotionScore>(name);

                        //var LH = emotionScore.tlemoScore;
                        //var LL = emotionScore.tremoScore;
                        //var RH = emotionScore.blemoScore;
                        //var RL = emotionScore.bremoScore;
                        var message = "{\"emo1\":" + "null" + ",\"emo2\":" + "null" + ",\"emo3\":" + "null" + ",\"emo4\":" + "null" + ",\"totalemo\":" + "null" + ",\"max\":1" + ",\"min\":-1,\"Gate\":0" + "}";
                        //Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, message);
                        eventhubclient.Send(new EventData(Encoding.UTF8.GetBytes(message)));
                        //return LH;
                    }
                    catch (Exception ex)
                    {
                        //return "   ";
                    }
                }
                //var eventhubclient = EventHubClient.CreateFromConnectionString(connectstr, hubname);
                //try
                //{
                //    //EmotionScore emotionScore = JsonConvert.DeserializeObject<EmotionScore>(name);

                //    //var LH = emotionScore.tlemoScore;
                //    //var LL = emotionScore.tremoScore;
                //    //var RH = emotionScore.blemoScore;
                //    //var RL = emotionScore.bremoScore;
                //    var message = "{\"emo1\":" + TopLeftEmo + ",\"emo2\":" + TopRightEmo + ",\"emo3\":" + BotLeftEmo + ",\"emo4\":" + BotRightEmo + ",\"max\":1" + ",\"min\":-1,\"Gate\":0" + "}";
                //    //Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, message);
                //    eventhubclient.Send(new EventData(Encoding.UTF8.GetBytes(message)));
                //    //return LH;
                //}
                //catch (Exception ex)
                //{
                //    //return "   ";
                //}
               
            }

        }
        class EmotionScore
        {
            public float totalScore { get; set; }
            public float tlemoScore { get; set; }
            public float tremoScore { get; set; }
            public float blemoScore { get; set; }
            public float bremoScore { get; set; }
        }
        class emotion
        {
            public float anger { get; set; }
            public float contempt { get; set; }
            public float disgust { get; set; }
            public float fear { get; set; }
            public float happiness { get; set; }
            public float neutral { get; set; }
            public float sadness { get; set; }
            public float surprise { get; set; }
        }
        class parameters
        {
            public int x { get; set; }
            public int y { get; set; }
            public float emo { get; set; }

        }
        

    }

}
