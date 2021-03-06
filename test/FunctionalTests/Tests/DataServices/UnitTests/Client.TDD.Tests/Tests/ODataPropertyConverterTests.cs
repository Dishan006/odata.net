﻿//---------------------------------------------------------------------
// <copyright file="ODataPropertyConverterTests.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace AstoriaUnitTests.TDD.Tests.Client
{
    using System;
    using System.Collections.Generic;
    using Microsoft.OData.Client;
    using Microsoft.OData.Client.Metadata;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Library;
    using Microsoft.OData.Core;
    using AstoriaUnitTests.TDD.Common;
    using Microsoft.OData.Edm.Library.Values;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests a subset of the serializer functionality in the client. More tests should be added as changes are made.
    /// </summary>
    [TestClass]
    public class ODataPropertyConverterTests
    {
        private ClientEdmModel clientModel;
        private EdmModel serverModel;
        private ODataPropertyConverter testSubject;
        private ClientTypeAnnotation clientTypeAnnotation;
        private DataServiceContext context;
        private EdmEntityType serverEntityType;
        private string serverEntityTypeName;
        private TestClientEntityType entity;
        private TestClientEntityType entityWithDerivedComplexProperty;
        private TestClientEntityType entityWithDerivedComplexInCollection;
        private string serverComplexTypeName;
        private string serverDerivedComplexTypeName;

        [TestInitialize]
        public void Init()
        {
            this.serverModel = new EdmModel();
            this.serverEntityType = new EdmEntityType("Server.NS", "ServerEntityType");
            this.serverEntityTypeName = "Server.NS.ServerEntityType";
            this.serverModel.AddElement(this.serverEntityType);
            this.serverEntityType.AddKeys(this.serverEntityType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32));
            
            var addressType = new EdmComplexType("Server.NS", "Address");
            addressType.AddStructuralProperty("Street", EdmPrimitiveTypeKind.String);
            this.serverEntityType.AddStructuralProperty("Address", new EdmComplexTypeReference(addressType, true));
            this.serverComplexTypeName = "Server.NS.Address";

            var homeAddressType = new EdmComplexType("Server.NS", "HomeAddress", addressType);
            homeAddressType.AddStructuralProperty("FamilyName", EdmPrimitiveTypeKind.String);
            this.serverComplexTypeName = "Server.NS.HomeAddress";

            var colorType = new EdmEnumType("Server.NS", "Color");
            colorType.AddMember(new EdmEnumMember(colorType, "Red", new EdmIntegerConstant(1)));
            colorType.AddMember(new EdmEnumMember(colorType, "Blue", new EdmIntegerConstant(2)));
            colorType.AddMember(new EdmEnumMember(colorType, "Green", new EdmIntegerConstant(3)));
            this.serverEntityType.AddStructuralProperty("Color", new EdmEnumTypeReference(colorType, false));

            this.serverEntityType.AddStructuralProperty("Nicknames", new EdmCollectionTypeReference(new EdmCollectionType(EdmCoreModel.Instance.GetInt32(false))));
            this.serverEntityType.AddStructuralProperty("OtherAddresses", new EdmCollectionTypeReference(new EdmCollectionType(new EdmComplexTypeReference(addressType, false))));

            this.clientModel = new ClientEdmModel(ODataProtocolVersion.V4);
            this.clientTypeAnnotation = this.clientModel.GetClientTypeAnnotation(typeof(TestClientEntityType));

            this.context = new DataServiceContext(new Uri("http://temp/"), ODataProtocolVersion.V4, this.clientModel);
            this.context.Format.UseJson(this.serverModel);

            this.testSubject = new ODataPropertyConverter(new RequestInfo(this.context));

            this.context.Format.UseJson(this.serverModel);
            this.context.ResolveName = t =>
                                       {
                                           if (t == typeof(TestClientEntityType))
                                           {
                                               return this.serverEntityTypeName;
                                           }

                                           if (t == typeof(Address))
                                           {
                                               return this.serverComplexTypeName;
                                           }

                                           return null;
                                       };
            this.entity = new TestClientEntityType
            {
                Id = 1, 
                Name = "foo", 
                Number = 1.23f, 
                Address = new Address(),
                OpenAddress = new Address(), 
                Nicknames = new string[0],
                OtherAddresses = new[] { new Address() },
                Color = 0
            };
            this.entityWithDerivedComplexProperty = new TestClientEntityType
            {
                Id = 1,
                Name = "foo",
                Number = 1.23f,
                Address = new HomeAddress(),
                OpenAddress = new Address(),
                Nicknames = new string[0],
                OtherAddresses = new[] { new Address() }
            };
            this.entityWithDerivedComplexInCollection = new TestClientEntityType
            {
                Id = 1,
                Name = "foo",
                Number = 1.23f,
                Address = new HomeAddress(),
                OpenAddress = new Address(),
                Nicknames = new string[0],
                OtherAddresses = new[] { new Address(), new HomeAddress() }
            };
        }

        [TestMethod]
        public void PrimitivePropertyThatIsNotDefinedOnTheServerShouldHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("Number");
            value.Should().HavePrimitiveValue(this.entity.Number).And.HaveSerializationTypeName("Edm.Single");
        }

        [TestMethod]
        public void PrimitivePropertyThatIsDefinedOnTheServerShouldNotHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("Id");
            value.Should().HavePrimitiveValue(this.entity.Id).And.NotHaveSerializationTypeName();
        }

        [TestMethod]
        public void PrimitivePropertyThatIsNullAndNotDefinedOnTheServerShouldNotHaveTypeAnnotation()
        {
            this.entity.Number = null;
            var value = this.ConvertSinglePropertyValue("Number");
            value.Should().BeODataNullValue().And.NotHaveSerializationTypeName();
        }

        [TestMethod]
        public void PrimitivePropertyWithMatchingJsonTypeAndNotDefinedOnTheServerShouldNotHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("Name");
            value.Should().HavePrimitiveValue(this.entity.Name).And.NotHaveSerializationTypeName();
        }

        [TestMethod]
        public void ComplexPropertyNotDefinedOnTheServerShouldHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("OpenAddress");
            value.Should().BeComplex().And.HaveSerializationTypeName(this.serverComplexTypeName);
        }

        [TestMethod]
        public void ComplexPropertyDefinedOnTheServerShouldHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("Address");
            value.Should().BeComplex().And.HaveSerializationTypeName(this.serverComplexTypeName);
        }

        [TestMethod]
        public void DerivedComplexPropertyDefinedOnTheServerShouldHaveDerivedTypeAnnotation()
        {
            var property = this.clientTypeAnnotation.GetProperty("Address", false);
            var results = this.testSubject.PopulateProperties(this.entityWithDerivedComplexProperty, this.serverEntityTypeName, new[] { property });
            results.Should().HaveCount(1);

            var odataProperty = results.Single();
            odataProperty.Name.Should().Be("Address");
            odataProperty.ODataValue.Should().BeComplex().And.HaveSerializationTypeName(this.serverDerivedComplexTypeName);
        }

        [TestMethod]
        public void EnumPropertyWithNonExistingValueShouldThrow()
        {
            Action action = () => this.ConvertSinglePropertyValue("Color");
            action.ShouldThrow<NotSupportedException>().WithMessage("The enum type 'Color' has no member named '0'.");
        }

        [TestMethod]
        public void CollectionPropertyDefinedOnTheServerShouldHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("Nicknames");
            value.Should().BeCollection().And.HaveSerializationTypeName("Collection(Edm.String)");
        }

        [TestMethod]
        public void ComplexInsideCollectionPropertyShouldHaveTypeAnnotation()
        {
            var value = this.ConvertSinglePropertyValue("OtherAddresses");
            value.Should().BeCollection();
            var collection = (ODataCollectionValue)value;
            collection.Items.Should().HaveCount(1);
            collection.Items.Cast<ODataValue>().Single().Should().BeComplex().And.HaveSerializationTypeName("Server.NS.HomeAddress");
        }

        [TestMethod]
        public void DerivedComplexInsideCollectionPropertyShouldNotThrow()
        {
            var property = this.clientTypeAnnotation.GetProperty("OtherAddresses", false);
            var results = this.testSubject.PopulateProperties(this.entityWithDerivedComplexInCollection, this.serverEntityTypeName, new[] { property });
            var value = results.Single().ODataValue;
            value.Should().BeCollection();
            var collection = (ODataCollectionValue)value;
            collection.Items.Should().HaveCount(2);
        }

        private ODataValue ConvertSinglePropertyValue(string propertyName)
        {
            var property = this.clientTypeAnnotation.GetProperty(propertyName, false);
            var results = this.testSubject.PopulateProperties(this.entity, this.serverEntityTypeName, new[] { property });
            results.Should().HaveCount(1);
            var odataProperty = results.Single();
            odataProperty.Name.Should().Be(propertyName);
            return odataProperty.ODataValue;
        }

        [Key("Id")]
        public class TestClientEntityType
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public ICollection<string> Nicknames { get; set; }
            public float? Number { get; set; }
            public Address Address { get; set; }
            public Address OpenAddress { get; set; }
            public ICollection<Address> OtherAddresses { get; set; }
            public Color Color { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }
        }

        public class HomeAddress : Address
        {
            public string FamilyName { get; set; }
        }
    }
}