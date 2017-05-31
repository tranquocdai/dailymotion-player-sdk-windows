﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using DMVideoPlayer.Annotations;


namespace DMVideoPlayer
{
    public class DMPlayerController : INotifyPropertyChanged
    {

        private static string defaultUrl = "https://www.dailymotion.com";
        private static bool defaultIsTapEnabled = true;

        private static string HockeyAppId = "6d380067c4d848ce863b232a1c5f10ae";
        private static string version = "2.9.3";
        private static string bundleIdentifier = "WindowsSDK";
        private static string eventName = "dmevent";
        private static string pathPrefix = "/embed/video/";
        private static string messageHandlerEvent = "triggerEvent";

        public event Action OnDmWebViewMessageUpdated;

        private string _baseUrl; // URL!
        public bool ApiReady { get; set; }
        public string VideoId { get; set; }
        // public string loadedJsonData { get; set; }
        public IDictionary<string, string> WithParameters { get; set; }
        public bool IsHeroVideo { get; set; }
        public bool PendingPlay { get; set; }
        public bool ShowingAd { get; set; }
        public string BaseUrl
        {
            get { return _baseUrl ?? defaultUrl; }
            set { _baseUrl = value; }
        }

        private bool? _isTapEnabled; // URL!

        public bool IsTapEnabled
        {
            get { return _isTapEnabled ?? defaultIsTapEnabled; }
            set { _isTapEnabled = value; }
        }

        private WebView _dmVideoPlayer;

        public WebView DmVideoPlayer
        {
            get { return _dmVideoPlayer; }
            set
            {

                _dmVideoPlayer = value;
                OnPropertyChanged();
            }
        }

        private string _dmWebViewMessage;

        public string DmWebViewMessage
        {
            get { return _dmWebViewMessage; }
            set
            {

                _dmWebViewMessage = value;
                OnDmWebViewMessageUpdated?.Invoke();
                OnPropertyChanged();
            }
        }

        /// Load a video with ID and optional OAuth token
        ///
        /// - Parameter videoId:        The video's XID
        /// - Parameter accessToken:    An optional oauth token. If provided it will be passed as Bearer token to the player.
        /// - Parameter withParameters: The list of configuration parameters that are passed to the player.
        public void Load(string videoId, string accessToken = "", IDictionary<string, string> withParameters = null)
        {

            this.VideoId = videoId;
            this.WithParameters = withParameters;

            //check base url
            if (BaseUrl != null)
            {
                //Creating a new webview when doing a new call
                if (DmVideoPlayer == null)
                {
                    DmVideoPlayer = NewWebView();

                    //setting cookies if needed
                    if (withParameters != null)
                    {
                        //if in params we have the keys v1st or tg then we need to send it to the player in a cookie
                        if (withParameters.ContainsKey("v1st"))
                        {
                            //set cookie
                            SetCookieInWebView("v1st", withParameters["v1st"]);
                        }
                        else if (withParameters.ContainsKey("ts"))
                        {
                            //set cookie
                            SetCookieInWebView("ts", withParameters["ts"]);
                        }
                        else if (withParameters.ContainsKey("clsu"))
                        {
                            //set cookie
                            SetCookieInWebView("clsu", withParameters["clsu"]);
                        }
                    }

                    //Recieving the events the player is sending
                    DmVideoPlayer.ScriptNotify += DmWebView_ScriptNotify;

                    //creating http request message to send to the webview
                    HttpRequestMessage request = NewRequest(videoId, accessToken, withParameters);

                    //doing call
                    DmVideoPlayer.NavigateWithHttpRequestMessage(request);
                }
                else
                    Load();
            }
        }

        public void Unload()
        {
            DmVideoPlayer.ScriptNotify -= DmWebView_ScriptNotify;
            DmVideoPlayer = null;
        }



        private void DmWebView_ScriptNotify(object sender, NotifyEventArgs e)
        {
            DmWebViewMessage = e?.Value;

            //switch (e?.Value.Contains)
            //{
            //    case "apiready":
            //        {
            //            ApiReady = true;
            //            if (PendingPlay)
            //            {
            //                PendingPlay = false;
            //                Load();
            //            }

            //            //Tracking.setPV5Info(map);
            //            break;
            //        }
            //    case "ad_start":
            //        {
            //            ShowingAd = true;
            //            break;
            //        }
            //    case "ad_end":
            //        {
            //            ShowingAd = false;
            //            break;
            //        }
            //}
        }

        private void Load()
        {
            //init Environment Info
            InitEnvironmentInfoVariables(WithParameters["jsonEnvironmentInfo"]);

            //mHasMetadata = false;
            if (WithParameters !=null && 
                WithParameters.ContainsKey("loadedJsonData"))
            {
                CallPlayerMethod("load", VideoId, WithParameters["loadedJsonData"]);
            }
            else
            {
                CallPlayerMethod("load", VideoId);
            }

            //if (IsHeroVideo)
            //{
            //    Mute();
            //}
            //else
            //{
            //    Unmute();
            //}
        }

        public void InitEnvironmentInfoVariables(string jsonData)
        {
            //PropData propData = new PropData();
            //propData.info = new Tracking.EnvironmentInfo();
            //propData.info.device = TDeviceInfo.get();
            //propData.info.app = TAppInfo.get();
            //propData.info.visitor = TVisitorInfo.create();
            CallPlayerMethod("setProp", "neon", jsonData);
        }

        private HttpRequestMessage NewRequest(string videoId, string accessToken = "", IDictionary<string, string> parameters = null)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, Url(videoId, parameters));

            if (accessToken != "")
            {
                message.Headers.Add("Authorization", "Bearer " + accessToken);
            }
            return message;
        }

        //Creating a new webview
        private WebView NewWebView()
        {
            //var webView = new WebView(WebViewExecutionMode.SeparateThread);
            var webView = new WebView(WebViewExecutionMode.SameThread);
            webView.IsTapEnabled = IsTapEnabled;

            webView.Opacity = 1;
            return webView;
        }


        private void SetCookieInWebView(string key, string value)
        {
            Uri baseUri = new Uri(defaultUrl);
            Windows.Web.Http.Filters.HttpBaseProtocolFilter filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();
            Windows.Web.Http.HttpCookie cookie = new Windows.Web.Http.HttpCookie(key, baseUri.Host, "/");
            cookie.Value = value;
            filter.CookieManager.SetCookie(cookie, false);
        }


        private Uri Url(string videoId, IDictionary<string, string> parameters = null)
        {
            var components = String.Concat(BaseUrl, pathPrefix, videoId);

            if (parameters == null)
            {
                parameters = new Dictionary<string, string>();
            }

            parameters["api"] = "nativeBridge";
            //parameters["objc_sdk_version"] = version;
            parameters["app"] = bundleIdentifier;
            //parameters["GK_PV5_ANTI_ADBLOCK"] = "0";
            parameters["GK_PV5_NEON"] = "1";

            var builder = new StringBuilder(components);
            if (parameters.Any())
                builder.Append("?");
            builder.Append(String.Join("&", from p in parameters select String.Format("{0}={1}", p.Key, p.Value)));

            return new Uri(builder.ToString());
        }

        public void ToggleControls(bool show)
        {
            var hasControls = show ? "1" : "0";
            NotifyPlayerApi(method: "controls", argument: hasControls);
        }

        private async void NotifyPlayerApi(string method, string argument = null)
        {

            string callingMethod = string.Format("player.api('{0}')", method);

            List<string> callingJsMethod = new List<string>();
            callingJsMethod.Add(callingMethod);

            //so sad
            var invokeScriptAsync = DmVideoPlayer?.InvokeScriptAsync("eval", callingJsMethod);
            if (invokeScriptAsync != null)
                await invokeScriptAsync;
        }

        // private async void CallEvalWebviewMethod(string callMethod)
        private async void CallEvalWebviewMethod(string callMethod)
        {
            List<string> callingJsMethod = new List<string>();
            callingJsMethod.Add(callMethod);
            var invokeScriptAsync = DmVideoPlayer?.InvokeScriptAsync("eval", callingJsMethod);
            if (invokeScriptAsync != null)
                await invokeScriptAsync;
        }

        public async void CallPlayerMethod(string method, string param, string dataJson = null)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("player.");
            builder.Append(method);
            builder.Append('(');
            builder.Append("'" + param + "'");

            if (dataJson != null)
            {
                builder.Append("JSON.parse('" + dataJson + "')");
            }

            builder.Append(')');
            String js = builder.ToString();

            CallEvalWebviewMethod(js);
        }

        public void setHeroVideo(bool isHeroVideo)
        {
            IsHeroVideo = isHeroVideo;
        }

        public bool isHeroVideo()
        {
            return IsHeroVideo;
        }

        public void ToggleFullscreen()
        {
            NotifyPlayerApi("notifyFullscreenChanged");
        }

        public void Play()
        {
            Debug.Write("PLAYER", "play");
            NotifyPlayerApi("play");
        }

        public void Pause()
        {
            Debug.Write("PLAYER", "pause");
            NotifyPlayerApi("pause");
        }


        public void Mute()
        {
            Debug.Write("PLAYER", "MUTE");
            NotifyPlayerApi("mute");
        }

        public void Unmute()
        {
            Debug.Write("PLAYER", "unmute");
            NotifyPlayerApi("unmute");
        }

        public void Volume(double value)
        {
            if (value >= 0.0 && value <= 1.0)
            {
                //NotifyPlayerApi("setVolume", value.ToString());
                NotifyPlayerApi(string.Format("setVolume({0})", value.ToString()));
            }
        }



        public void Seek(int seconds)
        {
            //player.seek(30);
            CallEvalWebviewMethod(string.Format("player.seek({0})", seconds));
            //NotifyPlayerApi(method: "seek", argument: "\(to)");
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
