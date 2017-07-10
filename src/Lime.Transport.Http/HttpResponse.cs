﻿using System;
using System.IO;
using System.Net;
using System.Text;
using Lime.Protocol;

namespace Lime.Transport.Http
{
    /// <summary>
    /// Encapsulates a HTTP
    /// response message.
    /// </summary>
    public sealed class HttpResponse
    {
        public HttpResponse(string correlatorId, HttpStatusCode statusCode, string statusDescription = null, WebHeaderCollection headers = null, MediaType contentType = null, Stream bodyStream = null, string body = null)
        {
            if (string.IsNullOrWhiteSpace(correlatorId))
            {
                throw new ArgumentException("CorrelatorId must be a valid string", nameof(correlatorId));
            }

            CorrelatorId = correlatorId;

            StatusCode = statusCode;
            StatusDescription = statusDescription;
            if (headers != null)
            {
                Headers = headers;
            }
            else
            {
                Headers = new WebHeaderCollection();
            }

            if (bodyStream == null &&
                body != null)
            {
                bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }
            
            if (bodyStream != null)
            {
                BodyStream = bodyStream;
                
                if (contentType != null)
                {
                    ContentType = contentType;
                }
                else
                {
                    ContentType = MediaType.Parse(Constants.TEXT_PLAIN_HEADER_VALUE);
                }

                Headers.Add(HttpResponseHeader.ContentType, ContentType.ToString());                
            }
        }

        public string CorrelatorId { get; private set; }

        public HttpStatusCode StatusCode { get; private set; }

        public string StatusDescription { get; private set; }

        public WebHeaderCollection Headers { get; private set; }

        public MediaType ContentType { get; private set; }


        public Stream BodyStream { get; private set; }
    }    
}
