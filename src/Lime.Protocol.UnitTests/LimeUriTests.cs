﻿using System;
using Xunit;
using Shouldly;

namespace Lime.Protocol.UnitTests
{
    
    public class LimeUriTests
    {        
        [Fact]
        public void Parse_ValidRelativeString_ReturnsInstance()
        {
            var resourceName = Dummy.CreateRandomString(10);
            var relativePath = string.Format("/{0}", resourceName);
            var actual = LimeUri.Parse(relativePath);

            actual.Path.ShouldNotBe(null);
            actual.Path.ShouldBe(relativePath);
            actual.IsRelative.ShouldBe(true);
        }

        [Fact]
        public void Parse_ValidAbsoluteString_ReturnsInstance()
        {
            var identity = Dummy.CreateIdentity();
            var resourceName = Dummy.CreateRandomString(10);
            var absolutePath = string.Format("{0}://{1}/{2}", LimeUri.LIME_URI_SCHEME, identity, resourceName);
            var actual = LimeUri.Parse(absolutePath);

            actual.Path.ShouldNotBe(null);
            actual.Path.ShouldBe(absolutePath);
            actual.IsRelative.ShouldBe(false);
        }

        [Fact]
        public void Parse_NullString_ThrowsArgumentNullException()
        {
            string path = null;
            Action action = () => LimeUri.Parse(path);
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Parse_InvalidRelativeString_ThrowsArgumentException()
        {
            var resourceName = Dummy.CreateRandomString(10);
            var invalidPath = string.Format("\\{0}", resourceName);            
            Action action = () => LimeUri.Parse(invalidPath);
            action.ShouldThrow<ArgumentException>();

        }

        [Fact]
        public void Parse_InvalidSchemeAbsoluteString_ThrowsArgumentException()
        {
            var absolutePath = "http://server@limeprotocol.org/presence";
            Action action = () => LimeUri.Parse(absolutePath);
            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void ToUri_AbsoluteInstance_ReturnsUri()
        {
            var identity = Dummy.CreateIdentity();
            var resourceName = Dummy.CreateRandomString(10);
            var absolutePath = string.Format("{0}://{1}/{2}", LimeUri.LIME_URI_SCHEME, identity, resourceName);
            var limeUri = LimeUri.Parse(absolutePath);
            
            // Act
            var uri = limeUri.ToUri();

            // Assert
            uri.Scheme.ShouldBe(LimeUri.LIME_URI_SCHEME);
            uri.UserInfo.ShouldBe(identity.Name);
            uri.Authority.ShouldBe(identity.Domain);
            uri.PathAndQuery.ShouldBe("/" + resourceName);
        }

        [Fact]
        public void ToUri_RelativeInstance_ThrowsInvalidOperationException()
        {
            var resourceName = Dummy.CreateRandomString(10);
            var relativePath = string.Format("/{0}", resourceName);
            var limeUri = LimeUri.Parse(relativePath);

            // Act
            Action action = () => limeUri.ToUri();
            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void ToUriIdentity_RelativeInstance_ReturnsUri()
        {
            var identity = Dummy.CreateIdentity();

            var resourceName = Dummy.CreateRandomString(10);
            var relativePath = string.Format("/{0}", resourceName);

            var limeUri = LimeUri.Parse(relativePath);

            // Act
            var uri = limeUri.ToUri(identity);

            // Assert
            uri.Scheme.ShouldBe(LimeUri.LIME_URI_SCHEME);
            uri.UserInfo.ShouldBe(identity.Name);
            uri.Authority.ShouldBe(identity.Domain);
            uri.PathAndQuery.ShouldBe("/" + resourceName);
        }

        [Fact]
        public void ToUriIdentity_AbsoluteInstance_ThrowsInvalidOperationException()
        {
            var identity = Dummy.CreateIdentity();
            var resourceName = Dummy.CreateRandomString(10);
            var absolutePath = string.Format("{0}://{1}/{2}", LimeUri.LIME_URI_SCHEME, identity, resourceName);
            var limeUri = LimeUri.Parse(absolutePath);

            // Act
            Action action = () => limeUri.ToUri(identity);
            action.ShouldThrow<InvalidOperationException>();
        }

    }
}
