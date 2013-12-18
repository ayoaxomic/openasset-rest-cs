﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using Newtonsoft.Json;

namespace OpenAsset.RestClient.Library
{
    public class ConnectionHelper
    {
        private string _serverURL = null;
        private string _username = null; // username 
        private string _password = null;
        private bool _anonymous = false;
        private string _sessionKey = null; //current session key
        private Error _lastError = null;

        //values from the last request made
        // if the last request didn't had the value it is empty
        public struct ResponseHeaders
        {
            public int? DisplayResultsCount;
            public int? FullResultsCount;
            public string OpenAssetVersion;
            public int? Offset;
            public string SessionKey; // last response session key (shouldn't be different from the current)
            //public int Timing; // only in development
            public int? UserId;
            public string Username;
        }
        public ResponseHeaders LastResponseHeaders;

        #region ConnectionHelper Factory
        private static Dictionary<string, ConnectionHelper> _connectionHelpers = new Dictionary<string, ConnectionHelper>();
        public static ConnectionHelper GetConnectionHelper(string serverURL, string username = null, string password = null)
        {
            ConnectionHelper connectionHelper = null;
            if (_connectionHelpers.ContainsKey(serverURL))
            {
                connectionHelper = _connectionHelpers[serverURL];
            }
            else
            {
                if (username == null && password == null)
                {
                    connectionHelper = new ConnectionHelper(serverURL);
                    connectionHelper._anonymous = true;
                }
                else
                {
                    connectionHelper = new ConnectionHelper(serverURL, username, password);
                }
                _connectionHelpers.Add(serverURL, connectionHelper);
            }
            //if URL exists but username and password different start a new session
            if (!connectionHelper._password.Equals(password) || !connectionHelper._username.Equals(username))
            {
                connectionHelper.NewSession(username, password);
            }
            return connectionHelper;
        }
        #endregion

        #region Constructors
        private ConnectionHelper(string serverURL)
        {
            _serverURL = serverURL;
            LastResponseHeaders = new ResponseHeaders();
        }

        private ConnectionHelper(string serverURL, string username, string password)
            : this(serverURL)
        {
            _username = username;
            _password = password;
        }
        #endregion

        #region Authorization
        private string authHeaderString(string username, string password)
        {
            return "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
        }

        private CredentialCache standardCredentials(string url)
        {
            CredentialCache cc = new CredentialCache();
            if (_anonymous)
                return cc;
            cc.Add(new Uri(url), "NTLM", CredentialCache.DefaultNetworkCredentials);
            if (!String.IsNullOrEmpty(_username) && !String.IsNullOrEmpty(_password))
            {
                cc.Add(new Uri(url), "Basic", new NetworkCredential(_username, _password));
            }
            return cc;
        }

        public void LogoutCurrentSession()
        {
            if (String.IsNullOrEmpty(_sessionKey))
                return;
            string validationUrl = _serverURL;
            validationUrl += Constant.REST_BASE_PATH + Constant.REST_AUTHENTICATE_URL_EXTENSION + Constant.REST_LOGOUT_EXTENSION;
            HttpWebResponse response = null;
            HttpWebRequest request = null;
            request = (HttpWebRequest)WebRequest.Create(validationUrl);
            request.Headers.Add(Constant.HEADER_SESSIONKEY, _sessionKey);
            request.Timeout = Constant.REST_AUTHENTICATE_TIMEOUT;
            request.UserAgent = Constant.REST_USER_AGENT;
            request.Method = "HEAD";
            try
            {
                response = getResponse(request);
            }
            catch (Exception)
            {
                // Doesn't matter if this fails for now
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        public bool NewSession(string username, string password)
        {
            _password = password;
            _username = username;
            return ValidateCredentials();
        }

        public bool ValidateCredentials(int retryIndex = 0)
        {
            string username = _username;
            string password = _password;
            string serverAddress = _serverURL;
            string sessionKey = _sessionKey;

            string validationUrl = serverAddress + Constant.REST_BASE_PATH + Constant.REST_AUTHENTICATE_URL_EXTENSION[retryIndex];
            HttpWebResponse response = null;
            HttpWebRequest request = null;

            request = (HttpWebRequest)WebRequest.Create(validationUrl);
            if (username == null || password == null)
            {
                request.Credentials = standardCredentials(validationUrl);
            }
            else if (!Constant.REST_ANONYMOUS_USERNAME.Equals(username))
            {
                request.Headers.Add("Authorization", authHeaderString(username, password));
            }
            if (!String.IsNullOrEmpty(sessionKey))
            {
                request.Headers.Add(Constant.HEADER_SESSIONKEY, sessionKey);
            }
            request.Timeout = Constant.REST_AUTHENTICATE_TIMEOUT;
            request.UserAgent = Constant.REST_USER_AGENT;
            request.Method = "HEAD";

            try
            {
                response = getResponse(request);
                //string validUser = response.Headers[Constant.HEADER_USERNAME];
                string validUser = LastResponseHeaders.Username;
                string lastSessionKey = LastResponseHeaders.SessionKey;

                //if (options != null)
                //options.OA_Version = response.Headers[Constant.HEADER_OPENASSET_VERSION];

                if (username == null || password == null)
                {
                    if (validUser == null || (!validUser.Equals(username) && !validUser.Equals(CredentialCache.DefaultNetworkCredentials.UserName)))
                    {
                        if (!String.IsNullOrEmpty(username) && !String.IsNullOrEmpty(password))
                        {
                            return ValidateCredentials();
                        }
                        else
                        {
                            return false;
                        }
                    }
                    //CurrentUsername = validUser;
                    //LastSuccessfulValidation = DateTime.Now;
                    return true;
                }

                if (validUser != null && validUser.Equals(username))
                {
                    // if it is a valid user keep the session
                    if (!String.IsNullOrEmpty(lastSessionKey))
                        _sessionKey = lastSessionKey;
                    //if (!String.IsNullOrEmpty(response.Headers[Constant.HEADER_SESSIONKEY]))
                    //_sessionKey = response.Headers[Constant.HEADER_SESSIONKEY];
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (WebException e)
            {
                if (httpRetryValid(request, e) || retryIndex < Constant.REST_AUTHENTICATE_URL_EXTENSION.Length)
                {
                    return ValidateCredentials(++retryIndex);
                }
                /*if (options != null && e.Response != null)
                {
                    options.OA_Version = e.Response.Headers[Constant.HEADER_OPENASSET_VERSION];
                }*/
                marshallError(validationUrl, e);
            }
            catch (Exception e)
            {
                marshallError(validationUrl, e);
            }
            finally
            {
                if (response != null)
                    response.Close();
            }

            return false;
        }
        #endregion

        #region Error handling
        private bool httpRetryValid(HttpWebRequest request, WebException we)
        {
            HttpWebResponse errorResponse = we.Response as HttpWebResponse;
            if (errorResponse == null)
                return false;
            bool anonLoginEnabled = Convert.ToBoolean(_anonymous);
            string username = null, password = null;
            string authorization = request.Headers["Authorization"];
            if (authorization != null && authorization.StartsWith("Basic "))
            {
                authorization = authorization.Substring(6);
                string[] credentials = System.Text.ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(authorization)).Split(new Char[] { ':' }, 2);
                if (credentials.Length == 2)
                {
                    username = credentials[0];
                    password = credentials[1];
                }
                else
                {
                    return true;
                }
            }
            if (authorization != null && authorization.StartsWith("NTLM "))
            {
                // Failed NTLM attempt, allow basic auth
                return true;
            }
            if (errorResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                if (!anonLoginEnabled && !String.IsNullOrEmpty(_username) && !String.IsNullOrEmpty(_password))
                {
                    if (!_username.Equals(errorResponse.Headers[Constant.HEADER_USERNAME]) && !_username.Equals(username) && !_password.Equals(password))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void marshallError(string openAssetUrl, Exception e)
        {
            if (e is WebException && (e as WebException).Status == WebExceptionStatus.ProtocolError)
            {
                HttpWebResponse errorResponse = (HttpWebResponse)(e as WebException).Response;
                setLastResponseHeaders(errorResponse.Headers);
                TextReader tr = new StreamReader(errorResponse.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();

                try
                {
                    _lastError = JsonConvert.DeserializeObject<Error>(responseText);
                }
                catch (JsonException)
                {
                    _lastError = new Error();
                    _lastError.http_status_code = (int)((e as WebException).Response as HttpWebResponse).StatusCode;
                    _lastError.error_message = responseText;
                }
            }
            else if (e is WebException)
            {
                _lastError = new Error();
                _lastError.http_status_code = (int)(e as WebException).Status;
                _lastError.error_message = e.Message;
            }
            else
            {
                _lastError = new Error();
                _lastError.http_status_code = -1;
                _lastError.error_message = e.Message;
            }

            throw new RESTAPIException(openAssetUrl, _lastError, e);
        }
        #endregion

        #region Response
        private HttpWebResponse getResponse(HttpWebRequest request, bool ignoreUsername = false)
        {
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            WebHeaderCollection responseHeader = response.Headers;
            string validUser = responseHeader[Constant.HEADER_USERNAME];
            if (!ignoreUsername)
            {
                if (!validUser.Equals(_username))
                {
                    string message = "Username of response differs from username in request";
                    throw new NotValidUserException(
                        response.ResponseUri.ToString(),
                        new Exception(message));
                }
            }
            setLastResponseHeaders(responseHeader);
            return response;
        }

        private void setLastResponseHeaders(WebHeaderCollection headerCollection)
        {
            LastResponseHeaders.OpenAssetVersion = headerCollection[Constant.HEADER_OPENASSET_VERSION];
            LastResponseHeaders.Username = headerCollection[Constant.HEADER_USERNAME];
            LastResponseHeaders.SessionKey = headerCollection[Constant.HEADER_SESSIONKEY];
            //LastRequestHeaders.Timing = Convert.ToInt32(headerCollection[Constant.HEADER_TIMING]);//development
            if (String.IsNullOrEmpty(headerCollection[Constant.HEADER_DISPLAY_RESULTS_COUNT]))
            {
                LastResponseHeaders.DisplayResultsCount = null;
            }
            else
            {
                LastResponseHeaders.DisplayResultsCount = Convert.ToInt32(headerCollection[Constant.HEADER_DISPLAY_RESULTS_COUNT]);
            }
            if (String.IsNullOrEmpty(headerCollection[Constant.HEADER_FULL_RESULTS_COUNT]))
            {
                LastResponseHeaders.FullResultsCount = null;
            }
            else
            {
                LastResponseHeaders.FullResultsCount = Convert.ToInt32(headerCollection[Constant.HEADER_FULL_RESULTS_COUNT]);
            }
            if (String.IsNullOrEmpty(headerCollection[Constant.HEADER_OFFSET]))
            {
                LastResponseHeaders.Offset = null;
            }
            else
            {
                LastResponseHeaders.Offset = Convert.ToInt32(headerCollection[Constant.HEADER_OFFSET]);
            }
            if (String.IsNullOrEmpty(headerCollection[Constant.HEADER_USER_ID]))
            {
                LastResponseHeaders.UserId = null;
            }
            else
            {
                LastResponseHeaders.UserId = Convert.ToInt32(headerCollection[Constant.HEADER_USER_ID]);
            }
        }

        private HttpWebResponse getRESTResponse(string url, string method, byte[] output = null, bool retry = false, bool isMultipart = false, string contentType = "application/json")
        {
            HttpWebResponse response = null;

            // HTTP REQUEST
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.UserAgent = Constant.REST_USER_AGENT;
            request.Timeout = Constant.REST_REQUEST_TIMEOUT;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.ContentType = contentType;

            if (!String.IsNullOrEmpty(_sessionKey))
            {
                request.Headers.Add(Constant.HEADER_SESSIONKEY, _sessionKey);
            }
            if (!_anonymous)
            {
                if (retry)
                {
                    request.Headers.Add("Authorization", authHeaderString(_username, _password));
                }
                else
                {
                    request.Credentials = standardCredentials(url);
                }
            }
            try
            {
                if (output != null && output.Length > 0)
                {
                    if (!isMultipart)
                    {
                        request.ContentLength = output.Length;
                        Stream requestStream = request.GetRequestStream();
                        requestStream.Write(output, 0, output.Length);
                        requestStream.Flush();
                        requestStream.Close();
                    }
                    else
                    {
                        request.ContentLength = output.Length;
                        // Send the form data to the request.
                        using (Stream requestStream = request.GetRequestStream())
                        {
                            requestStream.Write(output, 0, output.Length);
                            requestStream.Close();
                        }

                    }
                }
                response = getResponse(request, retry);
                if (!String.IsNullOrEmpty(LastResponseHeaders.SessionKey))
                {
                    _sessionKey = LastResponseHeaders.SessionKey;
                }
                //CurrentUsername = response.Headers[Constant.HEADER_USERNAME];
            }
            catch (WebException e)
            {
                if (httpRetryValid(request, e))
                {
                    return getRESTResponse(url, method, output, true, isMultipart, contentType);
                }
                marshallError(url, e);
                throw;
            }
            catch (Exception e)
            {
                marshallError(url, e);
                throw;
            }


            return response;
        }
        #endregion

        #region Multipart Form methods
        // Copied from: http://www.briangrinstead.com/blog/multipart-form-post-in-c
        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }

        //private static readonly Encoding encoding = Encoding.UTF8;
        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }
        
        // any base noun can be used but only the FileNoun accepts this type of POST
        public T SendObject<T>(T sendingObject, string filepath) where T : Noun.Base.BaseNoun, new()
        {
            // read file
            string filename = Path.GetFileName(filepath);
            string fileExtension = Path.GetExtension(filename).Remove(0,1);
            FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();
            // serialize sending object
            string jsonOut = JsonConvert.SerializeObject(sendingObject);//
            // generate post object
            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("file", new FileParameter(data, filename, "image/" + fileExtension));
            postParameters.Add("_jsonBody", jsonOut);
            
            // form data
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;
            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            HttpWebResponse response = null;
            try
            {
                string restUrl = _serverURL + Constant.REST_BASE_PATH + "/" + Noun.Base.BaseNoun.GetNoun(typeof(T));
                string method = "POST";

                //response = getRESTResponse(restUrl, method, output, true);
                response = getRESTResponse(restUrl, method, formData, true, true, contentType);
                T value = null;
                // get response data
                TextReader tr = new StreamReader(response.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();

                NewItem newItem = JsonConvert.DeserializeObject<NewItem>(responseText);
                value = new T();
                value.Id = newItem.NewId;

                return value;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }
        #endregion

        #region Get/Send objects
        public T GetObject<T>(int id, RESTOptions<T> options) where T : Noun.Base.BaseNoun, new()
        {
            HttpWebResponse response = null;
            try
            {
                string restUrl = _serverURL + Constant.REST_BASE_PATH + "/" + Noun.Base.BaseNoun.GetNoun(typeof(T)) + "/" + id + "?" + options.GetUrlParameters();
                response = getRESTResponse(restUrl, "GET");
                TextReader tr = new StreamReader(response.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();

                T objT = JsonConvert.DeserializeObject<T>(responseText);
                return objT;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        public List<T> GetObjects<T>(RESTOptions<T> options) where T : Noun.Base.BaseNoun, new()
        {
            return GetObjects<T>(0, null, options);
        }

        public List<T> GetObjects<T>(int id, string parentNoun, RESTOptions<T> options) where T : Noun.Base.BaseNoun, new()
        {
            HttpWebResponse response = null;
            try
            {
                string restUrl = _serverURL + Constant.REST_BASE_PATH;
                if (!String.IsNullOrEmpty(parentNoun))
                    restUrl += "/" + parentNoun;
                else
                    restUrl += "/" + Noun.Base.BaseNoun.GetNoun(typeof(T));
                if (id > 0)
                    restUrl += "/" + id;
                if (!String.IsNullOrEmpty(parentNoun))
                    restUrl += "/" + Noun.Base.BaseNoun.GetNoun(typeof(T));
                restUrl += "?" + options.GetUrlParameters();
                response = getRESTResponse(restUrl, "GET");

                //options.DisplayedResults = Convert.ToInt32(response.Headers[Constant.HEADER_DISPLAY_RESULTS_COUNT]);
                //options.TotalResults = Convert.ToInt32(response.Headers[Constant.HEADER_FULL_RESULTS_COUNT]);

                TextReader tr = new StreamReader(response.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();
                return JsonConvert.DeserializeObject<List<T>>(responseText);
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        public T SendObject<T>(T sendingObject, bool createNew = false) where T : Noun.Base.BaseNoun, new()
        {
            // serialize sending object
            string jsonOut = JsonConvert.SerializeObject(sendingObject);
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] output = encoding.GetBytes(jsonOut);
            // send post/put request
            HttpWebResponse response = null;
            try
            {
                string restUrl = _serverURL + Constant.REST_BASE_PATH + "/" + Noun.Base.BaseNoun.GetNoun(typeof(T));
                string method = "POST";
                if (!createNew)
                {
                    method = "PUT";
                    restUrl += "/" + sendingObject.Id;
                }

                response = getRESTResponse(restUrl, method, output, true, false);
                // get response data
                TextReader tr = new StreamReader(response.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();
                // fill value
                T value = null;
                if (createNew)
                {
                    NewItem newItem = JsonConvert.DeserializeObject<NewItem>(responseText);
                    value = new T();
                    value.Id = newItem.NewId;
                }
                else
                {
                    value = JsonConvert.DeserializeObject<T>(responseText);
                }
                return value;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        public List<T> SendObjects<T>(T sendingObject, bool createNew = false) where T : Noun.Base.BaseNoun, new()
        {
            // serialize sending object
            string jsonOut = JsonConvert.SerializeObject(sendingObject);
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] output = encoding.GetBytes(jsonOut);
            // send post/put request
            HttpWebResponse response = null;
            try
            {
                string restUrl = _serverURL + Constant.REST_BASE_PATH + "/" + Noun.Base.BaseNoun.GetNoun(typeof(T));
                string method = "POST";
                if (!createNew)
                {
                    method = "PUT";
                }
                response = getRESTResponse(restUrl, method, output, true, false);
                // get response data
                TextReader tr = new StreamReader(response.GetResponseStream());
                string responseText = tr.ReadToEnd();
                tr.Close();
                tr.Dispose();
                // fill values list
                List<T> values = null;
                if (createNew)
                {
                    List<NewItem> newItemList = JsonConvert.DeserializeObject<List<NewItem>>(responseText);
                    values = new List<T>();
                    foreach (NewItem newItem in newItemList)
                    {
                        T value = new T();
                        value.Id = newItem.NewId;
                        values.Add(value);
                    }
                }
                else
                {
                    values = JsonConvert.DeserializeObject<List<T>>(responseText);
                }
                return values;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }
        #endregion
    }
}
