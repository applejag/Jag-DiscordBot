using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class WebRequestHelper {
		public struct MyHttpWebResponse : IDisposable {
			public HttpWebResponse response;
			public WebException exception;

			public void Dispose() {
				response?.Dispose();
			}
		}

		public class MyHttpWebException : Exception {
			public HttpStatusCode StatusCode;

			public MyHttpWebException(HttpStatusCode StatusCode) {
				this.StatusCode = StatusCode;
			}

			public MyHttpWebException(string message, HttpStatusCode StatusCode) : base (message) {
				this.StatusCode = StatusCode;
			}

			public MyHttpWebException(string message, Exception inner, HttpStatusCode StatusCode) : base (message, inner) {
				this.StatusCode = StatusCode;
			}
		}
		
		public static async Task<MyHttpWebResponse> GetResponseAsyncNoException(this HttpWebRequest request) {
			try {
				return new MyHttpWebResponse { response = await request.GetResponseAsync() as HttpWebResponse, exception = null };
			} catch (WebException err) {
				HttpWebResponse response = err.Response as HttpWebResponse;
				if (response == null)
					throw;

				return new MyHttpWebResponse { response = response, exception = err };
			}
		}

		public static async Task<string> GetHTMLContent(this HttpWebRequest request) {

			using (MyHttpWebResponse myResponse = await request.GetResponseAsyncNoException()) {
				HttpWebResponse response = myResponse.response;

				if (response.StatusCode != HttpStatusCode.OK) {
					throw new MyHttpWebException("Response code " + (int) response.StatusCode + " (" + GetStatusCodeName((int)response.StatusCode) + ") while fetching from url:\n" + request.RequestUri, myResponse.exception, response.StatusCode);
				}
				Stream data = response.GetResponseStream();
				string html = string.Empty;
				using (StreamReader sr = new StreamReader(data)) {
					html = await sr.ReadToEndAsync();
				}

				return html;
			}
		}

		/// <summary>
		/// Makes a Http call towards <paramref name="url"/> and returns the response code.
		/// </summary>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="System.Security.SecurityException"></exception>
		/// <exception cref="UriFormatException"></exception>
		public static async Task<HttpStatusCode> TestUrlAsync(string url) {
			HttpWebRequest request = WebRequest.CreateHttp(url);
			using (MyHttpWebResponse myResponse = await request.GetResponseAsyncNoException()) {
				return myResponse.response.StatusCode;
			}
		}

		/// <summary>
		/// Get the status code description name from an integer statuscode
		/// <para>1xx: Informational - Request received, continuing process</para>
		/// <para>2xx: Success - The action was successfully received, understood, and accepted</para>
		/// <para>3xx: Redirection - Further action must be taken in order to complete the request</para>
		/// <para>4xx: Client Error - The request contains bad syntax or cannot be fulfilled</para>
		/// <para>5xx: Server Error - The server failed to fulfill an apparently valid request</para>
		/// </summary>
		public static string GetStatusCodeName(int StatusCode) {
			switch(StatusCode) {
				case 100: return "Continue";
				case 101: return "Switching Protocols";
				case 102: return "Processing";
				//case 103-199: return "Unassigned";
				case 200: return "OK";
				case 201: return "Created";
				case 202: return "Accepted";
				case 203: return "Non-Authoritative Information";
				case 204: return "No Content";
				case 205: return "Reset Content";
				case 206: return "Partial Content";
				case 207: return "Multi-Status";
				case 208: return "Already Reported";
				//case 209-225: return "Unassigned";
				case 226: return "IM Used";
				//case 227-299: return "Unassigned";
				case 300: return "Multiple Choices";
				case 301: return "Moved Permanently";
				case 302: return "Found";
				case 303: return "See Other";
				case 304: return "Not Modified";
				case 305: return "Use Proxy";
				case 306: return "(Unused)";
				case 307: return "Temporary Redirect";
				case 308: return "Permanent Redirect";
				//case 309-399: return "Unassigned";
				case 400: return "Bad Request";
				case 401: return "Unauthorized";
				case 402: return "Payment Required";
				case 403: return "Forbidden";
				case 404: return "Not Found";
				case 405: return "Method Not Allowed";
				case 406: return "Not Acceptable";
				case 407: return "Proxy Authentication Required";
				case 408: return "Request Timeout";
				case 409: return "Conflict";
				case 410: return "Gone";
				case 411: return "Length Required";
				case 412: return "Precondition Failed";
				case 413: return "Payload Too Large";
				case 414: return "URI Too Long";
				case 415: return "Unsupported Media Type";
				case 416: return "Range Not Satisfiable";
				case 417: return "Expectation Failed";
				//case 418-420: return "Unassigned";
				case 421: return "Misdirected Request";
				case 422: return "Unprocessable Entity";
				case 423: return "Locked";
				case 424: return "Failed Dependency";
				//case 425: return "Unassigned";
				case 426: return "Upgrade Required";
				//case 427:	return "Unassigned";
				case 428: return "Precondition Required";
				case 429: return "Too Many Requests";
				//case 430: return "Unassigned";
				case 431: return "Request Header Fields Too Large";
				//case 432-450: return "Unassigned";
				case 451: return "Unavailable For Legal Reasons";
				//case 452-499: return "Unassigned";
				case 500: return "Internal Server Error";
				case 501: return "Not Implemented";
				case 502: return "Bad Gateway";
				case 503: return "Service Unavailable";
				case 504: return "Gateway Timeout";
				case 505: return "HTTP Version Not Supported";
				case 506: return "Variant Also Negotiates";
				case 507: return "Insufficient Storage";
				case 508: return "Loop Detected";
				//case 509: return "Unassigned";
				case 510: return "Not Extended";
				case 511: return "Network Authentication Required";
				//case 512-599: return "Unassigned";
				default: return "Unassigned";
			}
		}
	}
}
