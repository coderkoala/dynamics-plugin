﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kipon.Solid.Plugin.UnitTests.Plugins.Contact
{
    [TestClass]
    public class ContactDeletePluginTest
    {
        [TestMethod]
        public void ExecuteTest()
        {
            using (var ctx = Kipon.Xrm.Fake.Repository.PluginExecutionFakeContext.ForType<Kipon.Solid.Plugin.Plugins.Contact.ContactDeletePlugin>())
            {
                var contact = new Entities.Contact { ContactId = Kipon.Solid.Plugin.Plugins.Contact.ContactDeletePlugin.TESTID };
                contact["fullname"] = "solid test";
                ctx.AddEntity(contact);

                ctx.Delete(new Microsoft.Xrm.Sdk.EntityReference(Entities.Contact.EntityLogicalName, contact.ContactId.Value));
            }

        }
    }
}
