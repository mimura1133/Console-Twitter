using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml;
using System.Linq;

namespace Console_Twitter
{
    /// <summary>
    /// TwitterLibrary の状況。
    /// </summary>
    enum TwitterLibraryStatus
    {
        /// <summary>
        /// 認証情報要求
        /// </summary>
        NeedPinOrToken,

        /// <summary>
        /// 認証失敗
        /// </summary>
        AuthorizationFailed,

        /// <summary>
        /// 認証成功 （処理可能）
        /// </summary>
        Authorized,

        /// <summary>
        /// 処理ミス。
        /// </summary>
        Failed,

        /// <summary>
        /// 成功
        /// </summary>
        Success
    }

    /// <summary>
    /// UserStream が受信された時に実行される。
    /// (削除の際は Status ID を除いた残りが全部 NULL となる。）
    /// </summary>
    /// <param name="statusid">Status ID</param>
    /// <param name="name">名前 : ex) みむら</param>
    /// <param name="screenname">ID : ex) mimura1133</param>
    /// <param name="text">つぶやき内容</param>
    delegate void Twitter_UserStream(string statusid, string screenname, string name, string text);

    class TwitterLibrary
    {
        #region Values

        public event Twitter_UserStream UserStreams;

        private Thread _userstreams_thread;

        private TwitterLibraryStatus _status;

        private string _request_token = "";
        private string _request_token_secret = "";
        private string _pin = "";

        private string _consumerkey = "";
        private string _consumersecret = "";

        private string _accesstoken = "";
        private string _accesstokensecret = "";

        private string _requesttoken_url = "https://api.twitter.com/oauth/request_token";
        private string _authorize_url = "https://twitter.com/oauth/authorize";
        private string _accesstoken_url = "https://api.twitter.com/oauth/access_token";
        private string _update_url = "https://api.twitter.com/1/statuses/update.xml";
        private string _follow_userstream_url = "https://userstream.twitter.com/2/user.json";
        private string _friends_list_url = "https://api.twitter.com/1/friends/ids.xml";
        private string _fav_url = "https://api.twitter.com/1/favorites/create/";
        private string _unfav_url = "https://api.twitter.com/1/favorites/destroy/";
        private string _retweet_url = "https://api.twitter.com/1/statuses/retweet/";
        private string _remove_url = "https://api.twitter.com/1/statuses/destroy/";

        private string _search_url = "https://search.twitter.com/search.json";
        private string _mentions_url = "https://api.twitter.com/1/statuses/mentions.json";
        private string _getfavorites_url = "https://api.twitter.com/1/favorites.json";
        private string _gettimeline_url = "https://api.twitter.com/1/statuses/home_timeline.json";
        private string _getusertimeline_url = "https://api.twitter.com/1/statuses/user_timeline.json";

        #endregion

        #region Properties

        /// <summary>
        /// 現在の認証状況
        /// </summary>
        public TwitterLibraryStatus Status
        {
            get { return this._status; }
        }

        /// <summary>
        /// PIN
        /// </summary>
        public string Pin
        {
            set { this._pin = value; }
        }

        /// <summary>
        /// Access Token
        /// </summary>
        public string AccessToken
        {
            set { this._accesstoken = value; }
            get { return this._accesstoken; }
        }

        /// <summary>
        /// Access Token Secret
        /// </summary>
        public string AccessTokenSecret
        {
            set { this._accesstokensecret = value; }
            get { return this._accesstokensecret; }
        }

        #endregion

        #region Functions

        public TwitterLibrary(string consumerkey, string consumersecret)
        {
            this._consumerkey = consumerkey;
            this._consumersecret = consumersecret;

            this._status = TwitterLibraryStatus.NeedPinOrToken;
            if (this._accesstokensecret != null && this._accesstoken != null)
            {
                this._status = TwitterLibraryStatus.Authorized;
                return;
            }
        }

        #region Public Function

        /// <summary>
        /// Twitter にログインを行う。
        /// </summary>
        /// <returns>認証成功かどうか。</returns>
        public bool Login()
        {
            string nonce = GetNonce();
            long time = GetTime();

            if (this._accesstokensecret != null && this._accesstoken != null)
            {
                this._status = TwitterLibraryStatus.Authorized;
                return true;
            }
            if (this._pin == "")
            {
                this._status = TwitterLibraryStatus.NeedPinOrToken;
                return false;
            }

            var signature = GetSignature(this._consumersecret, null,
                        "POST&" + UrlEncode(this._accesstoken_url) + "&" + GetParamString(this._consumerkey, nonce, null, time, this._request_token, this._pin, null));
            var hwr = (HttpWebRequest)HttpWebRequest.Create(this._accesstoken_url + "?" + GetParamString(this._consumerkey, nonce, signature, time, this._request_token, this._pin, null));
            hwr.Method = "POST";
            hwr.Headers.Add("Authorization", "OAuth");

            WebResponse webret;

            try
            {
                webret = hwr.GetResponse();
            }
            catch
            {
                this._status = TwitterLibraryStatus.AuthorizationFailed;
                return false;
            }

            string[] response = new StreamReader(webret.GetResponseStream()).ReadToEnd().Split('&');


            foreach (string s in response)
            {
                if (s.IndexOf("oauth_token=") != -1)
                    this._accesstoken = s.Substring(s.IndexOf("=") + 1);

                if (s.IndexOf("oauth_token_secret=") != -1)
                    this._accesstokensecret = s.Substring(s.IndexOf("=") + 1);
            }

            this._status = TwitterLibraryStatus.Authorized;

            return true;
        }

        /// <summary>
        /// PIN コード取得画面を出す。
        /// </summary>
        /// <returns>URL</returns>
        public string GetPinURL()
        {
            string nonce = GetNonce();
            long time = GetTime();

            var signature = GetSignature(this._consumersecret, null, "POST&" + UrlEncode(this._requesttoken_url) + "&" + UrlEncode(GetParamString(this._consumerkey, nonce, null, time, null, null, null)));
            var hwr = (HttpWebRequest)HttpWebRequest.Create(this._requesttoken_url + "?" + GetParamString(this._consumerkey, nonce, signature, time, null, null, null));
            hwr.Method = "POST";

            WebResponse webret;

            try
            {
                webret = hwr.GetResponse();
            }
            catch
            {
                this._status = TwitterLibraryStatus.AuthorizationFailed;
                return "";
            }

            string[] response = new StreamReader(webret.GetResponseStream()).ReadToEnd().Split('&');


            foreach (string s in response)
            {
                if (s.IndexOf("oauth_token=") != -1)
                    this._request_token = s.Substring(s.IndexOf("=") + 1);

                if (s.IndexOf("oauth_token_secret=") != -1)
                    this._request_token_secret = s.Substring(s.IndexOf("=") + 1);
            }

            this._status = TwitterLibraryStatus.NeedPinOrToken;
            return this._authorize_url + "?oauth_token=" + this._request_token;
        }

        /// <summary>
        /// つぶやく。
        /// </summary>
        /// <param name="str">つぶやき内容</param>
        /// <param name="reply_status_id">Reply を送るときの対象ID</param>
        /// <returns>成功かどうか</returns>
        public bool Update(string str, string reply_status_id = null)
        {
            string nonce = GetNonce();
            long time = GetTime();

            string tweet = UrlEncode(str);

            string signature = GetSignature(this._consumersecret, this._accesstokensecret,
                "POST&" + UrlEncode(this._update_url) + "&" + (reply_status_id != null ? ("in_reply_to_status_id%3D" + reply_status_id + "%26") : "") +
                "include_entities%3Dtrue" +
                "%26" + UrlEncode(GetParamString(_consumerkey, nonce, null, time, this._accesstoken, null, tweet)));

            HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(this._update_url);
            hwr.Method = "POST";
            hwr.ServicePoint.Expect100Continue = false;
            hwr.ContentType = "application/x-www-form-urlencoded";
            hwr.Headers.Add(HttpRequestHeader.Authorization, GetHeaderAuthorityString(this._consumerkey, nonce, signature, time, this._accesstoken));

            var message = new StreamWriter(hwr.GetRequestStream());
            message.Write("status=" + tweet + "&include_entities=true" + (reply_status_id != null ? ("&in_reply_to_status_id=" + reply_status_id) : ""));
            message.Close();

            try
            {
                var ret = hwr.GetResponse();
                ret.Close();
            }
            catch
            {
                this._status = TwitterLibraryStatus.Failed;
                return false;
            }

            this._status = TwitterLibraryStatus.Success;
            return true;
        }

        /// <summary>
        /// ふぁぼる。
        /// </summary>
        /// <param name="status_id">お気に入りに登録したいID</param>
        public bool Fav(string status_id)
        {
            var v = BasicTwitterAccess("POST", this._fav_url + status_id + ".xml");
            if (v != null)
                v.Close();

            return v != null;
        }

        /// <summary>
        /// ふぁぼ解除。
        /// </summary>
        /// <param name="status_id">お気に入りを解除したいID</param>
        public bool Unfav(string status_id)
        {
            var v = BasicTwitterAccess("POST", this._unfav_url + status_id + ".xml");
            if (v != null)
                v.Close();

            return v != null;
        }

        /// <summary>
        /// リツイート
        /// </summary>
        /// <param name="status_id">リツイートしたいID</param>
        public bool Retweet(string status_id)
        {
            var v = BasicTwitterAccess("POST", this._retweet_url + status_id + ".xml");
            if (v != null)
                v.Close();

            return v != null;
        }

        /// <summary>
        /// ツイート削除
        /// </summary>
        /// <param name="status_id">削除したいツイートのID</param>
        public bool Remove(string status_id)
        {
            var v = BasicTwitterAccess("POST", this._remove_url + status_id + ".xml");
            if (v != null)
                v.Close();

            return v != null;
        }

        /// <summary>
        /// ツイート検索
        /// </summary>
        /// <param name="query">検索</param>
        public void Search(string query)
        {
            HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(this._search_url + "?q=" + UrlEncode(query));
            hwr.Method = "GET";

            WebResponse r = null;

            try
            {
                r = hwr.GetResponse();
            }
            catch
            {
                this._status = TwitterLibraryStatus.Failed;
                return;
            }

            this._status = TwitterLibraryStatus.Success;

            if (r != null)
            {
                var stream = new StreamReader(r.GetResponseStream());
                var tweets = (new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(stream.ReadToEnd())["results"] as ArrayList)
                                                                            .ToArray().OrderBy(x => (x as Dictionary<string, object>)["id"]);
                foreach (var tweet in tweets)
                {
                    var info = tweet as Dictionary<string, object>;
                    UserStreams(info["id_str"] as string, info["from_user"] as string, info["from_user_name"] as string, info["text"] as string);
                }
           }
        }

        /// <summary>
        /// 直近２０件のリプライを取得する。
        /// </summary>
        public void GetMentions()
        {
            WebResponse r = BasicTwitterAccess("GET", this._mentions_url);
            if (r == null) return;

            if (r != null)
            {
                var stream = new StreamReader(r.GetResponseStream());
                var tweets = new JavaScriptSerializer().Deserialize<ArrayList>(stream.ReadToEnd());

                for (int i = tweets.Count - 1; i >= 0; i--)
                {
                    var tweet = tweets[i] as Dictionary<string, object>;

                    if (tweet.ContainsKey("user") && tweet.ContainsKey("text"))
                    {
                        var user = tweet["user"] as Dictionary<string, object>;
                        this.UserStreams(tweet["id_str"] as string, user["screen_name"] as string, user["name"] as string, tweet["text"] as string);
                    }
                }
            }
        }

        /// <summary>
        /// 直近２０件のタイムラインを取得する。
        /// </summary>
        public void GetTimeline()
        {
            WebResponse r = BasicTwitterAccess("GET", this._gettimeline_url);
            if (r == null) return;

            if (r != null)
            {
                var stream = new StreamReader(r.GetResponseStream());
                var tweets = new JavaScriptSerializer().Deserialize<ArrayList>(stream.ReadToEnd());

                for (int i = tweets.Count - 1; i >= 0; i--)
                {
                    var tweet = tweets[i] as Dictionary<string, object>;

                    if (tweet.ContainsKey("user") && tweet.ContainsKey("text"))
                    {
                        var user = tweet["user"] as Dictionary<string, object>;
                        this.UserStreams(tweet["id_str"] as string, user["screen_name"] as string, user["name"] as string, tweet["text"] as string);
                    }
                }
            }
        }

        /// <summary>
        /// 直近２０件のふぁぼを取得する
        /// </summary>
        public void GetFavorites()
        {
            WebResponse r = BasicTwitterAccess("GET", this._getfavorites_url);
            if (r == null) return;

            if (r != null)
            {
                var stream = new StreamReader(r.GetResponseStream());
                var tweets = new JavaScriptSerializer().Deserialize<ArrayList>(stream.ReadToEnd());

                for (int i = tweets.Count - 1; i >= 0; i--)
                {
                    var tweet = tweets[i] as Dictionary<string, object>;

                    if (tweet.ContainsKey("user") && tweet.ContainsKey("text"))
                    {
                        var user = tweet["user"] as Dictionary<string, object>;
                        this.UserStreams(tweet["id_str"] as string, user["screen_name"] as string, user["name"] as string, tweet["text"] as string);
                    }
                }
            }
        }

        /// <summary>
        /// 指定ユーザの直近２０件のタイムラインを取得する。
        /// <param name="screenname">Twitter ID ex.) mimura1133</param>
        /// </summary>
        public void GetUserTimeline(string screenname)
        {
            HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(this._getusertimeline_url + "?include_entities=true&screen_name=" + screenname);
            hwr.Method = "GET";

            WebResponse r = null;

            try
            {
                r = hwr.GetResponse();
            }
            catch
            {
                this._status = TwitterLibraryStatus.Failed;
                return;
            }

            this._status = TwitterLibraryStatus.Success;

            var stream = new StreamReader(r.GetResponseStream());
            var tweets = new JavaScriptSerializer().Deserialize<ArrayList>(stream.ReadToEnd());

            for (int i = tweets.Count - 1; i >= 0; i--)
            {
                var tweet = tweets[i] as Dictionary<string, object>;

                if (tweet.ContainsKey("user") && tweet.ContainsKey("text"))
                {
                    var user = tweet["user"] as Dictionary<string, object>;
                    this.UserStreams(tweet["id_str"] as string, user["screen_name"] as string, user["name"] as string, tweet["text"] as string);
                }
            }
        }

        /// <summary>
        /// UserStream 受信を開始する。
        /// </summary>
        public void BeginUserStream()
        {
            // Friends 一覧をとってきて、UserStream で取得すべきユーザを決定する。
            WebResponse c;
            do
            {
                c = BasicTwitterAccess("GET", this._friends_list_url);
                if (c == null)
                    Thread.Sleep(300);
            } while (c == null);

            var stream = new StreamReader(c.GetResponseStream());
            var list = stream.ReadToEnd();

            stream.Close();

            var doc = new XmlDocument();
            doc.LoadXml(list);

            var builder = new StringBuilder();

            foreach (XmlElement id in doc["id_list"]["ids"].ChildNodes)
            {
                builder.Append(id.InnerText + ",");
            }

            builder.Append(this._accesstoken.Substring(0, this._accesstoken.IndexOf("-")));

            //
            // この辺参照：
            // https://dev.twitter.com/docs/streaming-api/user-streams
            //
            // Debug : 
            // string query = "{\"friends\":[" + builder + "]}";
            //

            this._userstreams_thread = new Thread(this.GetUserStream);
            this._userstreams_thread.Start("{\"friends\":[" + builder + "]}");
        }

        /// <summary>
        /// UserStream 受信を停止する。
        /// </summary>
        public void EndUserStream()
        {
            this._userstreams_thread.Abort();
        }

        /// <summary>
        /// UserStream 受信処理。
        /// </summary>
        private void GetUserStream(object o)
        {
            while (true)
            {
                WebResponse r;
                {
                    int counter = 0;
                    do
                    {
                        r = BasicTwitterAccess("GET", this._follow_userstream_url);
                        if (r == null)
                        {
                            Thread.Sleep(2000);
                            counter++;
                            if (counter == 2)
                            {
                                // UserStream が２回失敗した場合は、通常取得を１回行う。
                                this.GetTimeline();
                                counter = 0;
                            }
                        }
                    } while (r == null);
                }
                var stream = new StreamReader(r.GetResponseStream());

                while (true)
                {
                    try
                    {
                        var text = stream.ReadLine();
                        if (text != null && this.UserStreams != null)
                        {
                            if (text.Length > 0)
                            {
                                var tweet = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
                                if (tweet.ContainsKey("user") && tweet.ContainsKey("text"))
                                {
                                    var user = tweet["user"] as Dictionary<string, object>;
                                    this.UserStreams(tweet["id_str"] as string, user["screen_name"] as string, user["name"] as string, tweet["text"] as string);
                                }
                                else if (tweet.ContainsKey("delete"))
                                {
                                    var info = (tweet["delete"] as Dictionary<string, object>)["status"] as Dictionary<string, object>;
                                    this.UserStreams(info["id_str"] as string, null, null, null);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch { break; }
                }
                try { r.Close(); }
                catch { }
            }
        }

        /// <summary>
        /// Twitter と通信をする。
        /// </summary>
        /// <param name="method">通信メソッド</param>
        /// <param name="url">URL</param>
        /// <param name="query">クエリ文字列</param>
        /// <returns></returns>
        private WebResponse BasicTwitterAccess(string method, string url)
        {
            string nonce = GetNonce();
            long time = GetTime();

            string signature = GetSignature(this._consumersecret, this._accesstokensecret,
                method + "&" + UrlEncode(url) + "&" + UrlEncode(GetParamString(_consumerkey, nonce, null, time, this._accesstoken, null, null)));

            HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(url);
            hwr.Method = method;
            hwr.Headers.Add(HttpRequestHeader.Authorization, GetHeaderAuthorityString(this._consumerkey, nonce, signature, time, this._accesstoken));
            hwr.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            WebResponse r = null;

            try
            {
                r = hwr.GetResponse();
            }
            catch
            {
                this._status = TwitterLibraryStatus.Failed;
            }

            this._status = TwitterLibraryStatus.Success;
            return r;
        }

        #endregion

        #region Utility

        /// <summary>
        /// ランダム数字列を取得
        /// </summary>
        /// <returns>ランダム数字列</returns>
        private static string GetNonce()
        {
            Random rand = new Random();
            byte[] b = new byte[32];

            rand.NextBytes(b);

            return Math.Abs(BitConverter.ToInt64(b, 0)).ToString();
        }

        /// <summary>
        /// UNIX 時間を取得
        /// </summary>
        /// <returns>UNIX 時間</returns>
        private static long GetTime()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        /// <summary>
        /// URL エンコードをする。
        /// </summary>
        /// <param name="str">変換対象文字列</param>
        /// <returns>エンコード済み文字列</returns>
        private static string UrlEncode(string str)
        {
            string s = HttpUtility.UrlEncode(str).Replace("+", "%20");
            return UrlEncodeUpper(s);
        }

        /// <summary>
        /// URL エンコード結果を大文字に直す。
        /// </summary>
        /// <param name="str">エンコード済み文字列</param>
        /// <returns>処理後文字列</returns>
        private static string UrlEncodeUpper(string str)
        {
            int p = str.IndexOf("%");
            if (p != -1)
            {
                str = str.Substring(0, p) + str.Substring(p, 3).ToUpper() + UrlEncodeUpper(str.Substring(p + 3));
            }
            return str;
        }

        /// <summary>
        /// パラメータ文字列を作成。
        /// </summary>
        /// <param name="oauth_consumer_key">Consumer Key</param>
        /// <param name="oauth_nonce">Notice</param>
        /// <param name="oauth_signature">Signature</param>
        /// <param name="oauth_timestamp">TimeStamp</param>
        /// <param name="oauth_token">Token</param>
        /// <param name="oauth_verifier">Verifier (PIN)</param>
        /// <param name="status">Status</param>
        /// <returns>パラメータ文字列</returns>
        private static string GetParamString(string oauth_consumer_key, string oauth_nonce, string oauth_signature, long oauth_timestamp, string oauth_token, string oauth_verifier, string status)
        {
            string param;

            param = "oauth_consumer_key=" + oauth_consumer_key + "&oauth_nonce=" + oauth_nonce;
            if (oauth_signature != null) param += "&oauth_signature=" + oauth_signature;
            param += "&oauth_signature_method=HMAC-SHA1" + "&oauth_timestamp=" + oauth_timestamp;
            if (oauth_token != null) param += "&oauth_token=" + oauth_token;
            if (oauth_verifier != null) param += "&oauth_verifier=" + oauth_verifier;
            param += "&oauth_version=1.0";
            if (status != null) param += "&status=" + status;

            return param;
        }

        /// <summary>
        /// ヘッダにつける認証情報の文字列を生成。
        /// </summary>
        /// <param name="oauth_consumer_key">Consumer Key</param>
        /// <param name="oauth_nonce">Notice</param>
        /// <param name="oauth_signature">Signature</param>
        /// <param name="oauth_timestamp">TimeStamp</param>
        /// <param name="oauth_token">Token</param>
        /// <returns>認証情報</returns>
        private static string GetHeaderAuthorityString(string oauth_consumer_key, string oauth_nonce, string oauth_signature, long oauth_timestamp, string oauth_token)
        {
            return "OAuth oauth_consumer_key=\"" + UrlEncode(oauth_consumer_key) + "\",oauth_signature_method=\"HMAC-SHA1\"," +
                   "oauth_timestamp=\"" + oauth_timestamp + "\",oauth_nonce=\"" + UrlEncode(oauth_nonce) + "\"," +
                   "oauth_version=\"1.0\",oauth_token=\"" + UrlEncode(oauth_token) + "\"," +
                   "oauth_signature=\"" + UrlEncode(oauth_signature) + "\",";
        }


        /// <summary>
        /// シグネチャを作成
        /// </summary>
        /// <param name="consumer_secret">Consumer Secret</param>
        /// <param name="access_secret">Token Secret</param>
        /// <param name="param">パラメータ文字列</param>
        /// <returns>シグネチャ</returns>
        private static string GetSignature(string consumer_secret, string access_secret, string param)
        {
            HMACSHA1 hmacsha1 = new HMACSHA1();
            hmacsha1.Key = Encoding.ASCII.GetBytes(consumer_secret + "&" + access_secret);
            byte[] hash = hmacsha1.ComputeHash(Encoding.ASCII.GetBytes(param));

            return Convert.ToBase64String(hash);
        }

        #endregion

        #endregion
    }
}
