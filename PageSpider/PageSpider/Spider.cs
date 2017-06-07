using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PageSpider
{
    public class Spider
    {

        #region private type

        private class RequestState
        {
            private const int BUFFER_SIZE = 131072; //1024 * 128 = 128K
            private byte[] _data = new byte[BUFFER_SIZE];
            private StringBuilder sb = new StringBuilder();

            public HttpWebRequest Req { get; private set; }
            public string Url { get; private set; }
            public int Depth { get; private set; }
            public int Index { get; private set; }

            public Stream ResStream { get; set; }

            public StringBuilder Html
            {
                get { return sb; }
            }

            public byte[] Data
            {
                get { return _data; }
            }

            public int BufferSize
            {
                get { return BUFFER_SIZE; }
            }

            public RequestState(HttpWebRequest req, string url, int depth, int index)
            {
                Req = req;
                Url = url;
                Depth = depth;
                Index = index;
            }
        }

        private class WorkingUnitCollection
        {
            private int _count;
            private bool[] busy;
            public WorkingUnitCollection(int count)
            {
                _count = count;
                busy = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    busy[i] = true;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="index"></param>
            public void StartWorking(int index)
            {
                if (!busy[index])
                {
                    busy[index] = true;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="index"></param>
            public void FinishWorking(int index)
            {
                if (busy[index])
                {
                    busy[index] = false;
                }
            }

            /// <summary>
            /// 判断工作是否完成
            /// </summary>
            /// <returns></returns>
            public bool IsFinished()
            {
                bool isEnd = false;
                foreach (bool b in busy)
                {
                    isEnd |= b; 
                }
                return !isEnd;
            }

            /// <summary>
            /// 等待所有工作完成
            /// </summary>
            public void WaitAllFinished()
            {
                while (true)
                {
                    if (IsFinished())
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }
            }

            /// <summary>
            /// 终止所有工作
            /// </summary>
            public void AbortAllWork()
            {
                for (int i = 0; i < _count; i++)
                {
                    busy[i] = false;
                }
            }
        }

        #endregion

        #region private field

        private static Encoding GB18030 = Encoding.GetEncoding("GB18030");
        private static Encoding UTF8 = Encoding.UTF8;
        private string userAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
        private string accept = "text/html";
        private string method = "GET";
        private Encoding encoding = GB18030;
        private Encodings enc = Encodings.GB;
        private int maxTime = 2 * 60 * 1000;

        private int index;
        private string path = null;
        private int maxDepth = 2;
        private int maxExternaldepth = 0;
        private string _rootUrl = null;
        private string _baseUrl = null;
        private Dictionary<string, int> urlsLoaded = new Dictionary<string, int>();//已下载集合
        private Dictionary<string, int> urlsUnload = new Dictionary<string, int>();//未下载集合

        private bool isStop = true;
        private Timer checkTimer = null;
        private readonly object locker = new object();
        private bool[] reqBusy = null;
        private int reqCount = 4;
        private WorkingUnitCollection workingSignals;

        #endregion

        #region constructors

        public Spider()
        {
            //
        }

        #endregion

        #region properties

        /// <summary>
        /// 根Url 返回类似http://www.baidu.com的RootUrl和类似baidu.com的baseUrl
        /// </summary>
        public string RootUrl
        {
            get
            {
                return _rootUrl;
            }
            set
            {
                if (!value.Contains("http://"))
                {
                    _rootUrl = "http://" + value;
                }
                else
                {
                    _rootUrl = value;
                }
                _baseUrl = _rootUrl.Replace("www.", "");
                _baseUrl = _baseUrl.Replace("http://", "");
                _baseUrl = _baseUrl.TrimEnd('/');
            }
        }

        /// <summary>
        /// 页面编码方式
        /// </summary>
        public Encodings PageEncoding
        {
            get { return enc; }
            set
            {
                enc = value;
                switch (value)
                {
                    case Encodings.GB:
                        encoding = GB18030;
                        break;
                    case Encodings.UTF8:
                        encoding = UTF8;
                        break;
                }
            }
        }

        /// <summary>
        ///最大下载深度 最小值1
        /// </summary>
        public int MaxDepth
        {
            get { return maxDepth; }
            set
            {
                maxDepth = Math.Max(value, 1);
            }
        }

        /// <summary>
        /// 下载最大连接数
        /// </summary>
        public int MaxConnection
        {
            get { return reqCount; }
            set
            {
                reqCount = value;
            }
        }

        #endregion

        #region public type

        public delegate void ContentsSavedHandler(string path, string url);
        public delegate void DownloadFinishHandler(int count);

        public enum Encodings
        {
            UTF8,
            GB
        }

        #endregion

        #region events

        /// <summary>
        /// 正文内容被保存到本地后触发
        /// </summary>
        public ContentsSavedHandler ContentsSaved = null;
        /// <summary>
        /// 全部链接下载分析完毕后触发
        /// </summary>
        public DownloadFinishHandler DownloadFinish = null;

        #endregion

        #region public methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Download(string path)
        {
            if (string.IsNullOrEmpty(RootUrl))
            {
                return;
            }
            this.path = path;
            Init();
            StartDownload();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Abort()
        {
            isStop = true;
            if (workingSignals != null)
            {
                workingSignals.AbortAllWork();
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// 开始下载
        /// </summary>
        private void StartDownload()
        {
            //每0.3秒检查是否完成
            checkTimer = new Timer(new TimerCallback(CheckFinish), null, 0, 300);
            DispatchWork();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        private void CheckFinish(object param)
        {
            if (workingSignals.IsFinished())
            {
                checkTimer.Dispose();
                checkTimer = null;
                DownloadFinish?.Invoke(index);
            }
        }

        private void Init()
        {
            urlsLoaded.Clear();
            urlsUnload.Clear();
            AddUrls(new string[1] { RootUrl }, 0);
            index = 0;
            reqBusy = new bool[reqCount];
            workingSignals = new WorkingUnitCollection(reqCount);
            isStop = false;
        }

        private void DispatchWork()
        {
            if (isStop)
            {
                return;
            }
            for (int i = 0; i < reqCount; i++)
            {
                if (!reqBusy[i])
                {
                    RequestResource(i);
                }
            }
        }

        private void RequestResource(int index)
        {
            int depth;
            string url = "";
            try
            {
                lock (locker)
                {
                    if (urlsUnload.Count <= 0)
                    {
                        workingSignals.FinishWorking(index);
                        return;
                    }
                    reqBusy[index] = true;
                    workingSignals.StartWorking(index);
                    depth = urlsUnload.First().Value;
                    url = urlsUnload.First().Key;
                    urlsLoaded.Add(url, depth);
                    urlsUnload.Remove(url);
                }
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Accept = accept;
                req.UserAgent = userAgent;
                req.Method = method;
                RequestState state = new RequestState(req, url, depth, index);
                var result = req.BeginGetResponse(new AsyncCallback(ReceivedResource), state);
                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallBack, state, maxTime, true);
            }
            catch (WebException ex)
            {
                System.Windows.MessageBox.Show("RequestResource " + ex.Message + url + ex.Status);
            }
        }

        private void ReceivedResource(IAsyncResult result)
        {

            RequestState state = (RequestState)result.AsyncState;
            HttpWebRequest req = state.Req;
            string url = state.Url;
            try
            {
                HttpWebResponse res = (HttpWebResponse)req.EndGetResponse(result);
                if (isStop)
                {
                    res.Close();
                    req.Abort();
                    return;
                }
                if (res != null && res.StatusCode == HttpStatusCode.OK)
                {
                    Stream stream = res.GetResponseStream();
                    state.ResStream = stream;
                    var re = stream.BeginRead(state.Data, 0, state.BufferSize, new AsyncCallback(ReceivedData), state);
                }
                else
                {
                    res.Close();
                    state.Req.Abort();
                    reqBusy[state.Index] = false;
                    DispatchWork();
                }
            }
            catch (WebException ex)
            {
                System.Windows.MessageBox.Show("ReceivedResource " + ex.Message + url + ex.Status);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void ReceivedData(IAsyncResult result)
        {
            RequestState state = (RequestState)result.AsyncState;
            HttpWebRequest request = state.Req;
            Stream stream = state.ResStream;
            string url = state.Url;
            int depth = state.Depth;
            int index = state.Index;
            string html = null;
            int read = 0;
            try
            {
                read = stream.EndRead(result);
                if (isStop)
                {
                    state.ResStream.Close();
                    request.Abort();
                    return;
                }
                if (read > 0)
                {
                    MemoryStream ms = new MemoryStream(state.Data, 0, read);
                    StreamReader reader = new StreamReader(ms, Encoding.UTF8);
                    string str = reader.ReadToEnd();
                    state.Html.Append(str);
                    var ar = stream.BeginRead(state.Data, 0, state.BufferSize, new AsyncCallback(ReceivedData), state);
                    return;
                }
                html = state.Html.ToString();
                SaveContents(html, url);
                string[] links = GetLinks(html);
                AddUrls(links, depth + 1);

                reqBusy[index] = false;
                DispatchWork();
            }
            catch (WebException ex)
            {
                System.Windows.MessageBox.Show("ReceivedData Web " + ex.Message + url + ex.Status);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void TimeoutCallBack(object state, bool timeOut)
        {
            if (timeOut)
            {
                RequestState req = state as RequestState;
                if (req != null)
                {
                    req.Req.Abort();
                }
                reqBusy[req.Index] = false;
                DispatchWork();
            }
        }

        private string[] GetLinks(string html)
        {
            const string pattern = @"http://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            Regex r = new Regex(pattern);
            MatchCollection mc = r.Matches(html);
            string[] links = new string[mc.Count];

            for (int i = 0; i < mc.Count; i++)
            {
                links[i] = mc[i].ToString();
            }
            return links;
        }

        /// <summary>
        /// 判断url是否在已下载队列中
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool UrlExists(string url)
        {
            bool result = urlsUnload.ContainsKey(url);
            result |= urlsLoaded.ContainsKey(url);
            return result;
        }

        /// <summary>
        /// 判断url是否可用：在已下载队列或者为其他后缀名不可用
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool UrlAvaliable(string url)
        {
            if (UrlExists(url))
            {
                return false;
            }
            if (url.Contains(".jpg") || url.Contains(".gif")
                || url.Contains(".png") || url.Contains(".css")
                || url.Contains(".js"))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="depth"></param>
        private void AddUrls(string[] urls, int depth)
        {
            if (depth >= maxDepth)
            {
                return;
            }
            foreach (string url in urls)
            {
                string cleanUrl = url.Trim();
                int end = cleanUrl.IndexOf(' ');
                if (end > 0)
                {
                    cleanUrl = cleanUrl.Substring(0, end);
                }
                cleanUrl = cleanUrl.TrimEnd('/');
                if (UrlAvaliable(cleanUrl))
                {
                    if (cleanUrl.Contains(_baseUrl))
                    {
                        urlsUnload.Add(cleanUrl, depth);
                    }
                    else
                    {
                        //外链
                    }
                }
            }
        }

        private void SaveContents(string html, string url)
        {
            if (string.IsNullOrEmpty(html))
            {
                return;
            }
            string path = "";
            lock (locker)
            {
                path = $"{this.path}\\{index++}.txt";
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.Write(html);
                }
            }
            catch (IOException ioe)
            {
                System.Windows.MessageBox.Show("SaveContents IO" + ioe.Message + " path=" + path);
            }
            ContentsSaved?.Invoke(path, url);
        }

        #endregion
    }
}
