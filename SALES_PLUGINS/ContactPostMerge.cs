namespace VEER.Plugins
{
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Metadata;
    using System.Linq;
    using System.Runtime.Remoting.Services;
    using System.Text;
    using System.Threading.Tasks;
    public class ContactPostMerge : IPlugin
    {
        IOrganizationService service = null;
        ITracingService tracingService = null;
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                service = serviceFactory.CreateOrganizationService(context.UserId);
                tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                tracingService.Trace("ContactPostMerge");


                var primaryEntityId = context.PrimaryEntityId;

                Entity contact = service.Retrieve("contact", primaryEntityId, new ColumnSet("statecode", "masterid"));

                EntityReference masterid = contact.Contains("masterid") && contact["masterid"] != null ?
                    contact.GetAttributeValue<EntityReference>("masterid") : null;

                OptionSetValue stateCodeOptionSet = contact.GetAttributeValue<OptionSetValue>("statecode");

                // Get the integer value of the statecode
                int stateCodeValue = stateCodeOptionSet.Value;

                if (stateCodeValue != 1)
                {
                    tracingService.Trace("return from plugin ContactPostMerge due to contact statecode is not inactive");
                    return;
                }
                if (masterid == null)
                {
                    tracingService.Trace("masterid is null return from plugin contactPostMerge");
                    return;
                }

                //var request = new MergeRequest { Parameters = executionContext.InputParameters };
                //Implementation continues here

                //tracingService.Trace("request.Parameters = " + request.Parameters);
                //tracingService.Trace("request.PerformParentingChecks = " + request.PerformParentingChecks);
                //tracingService.Trace("request.RequestId = " + request.RequestId);
                //tracingService.Trace("request.RequestName = " + request.RequestName);
                //tracingService.Trace("request.SubordinateId = " + request.SubordinateId);
                //tracingService.Trace("request.Target.id = " + request.Target.Id);
                //tracingService.Trace("request.Target.Name = " + request.Target.Name);
                //tracingService.Trace("request.UpdateContent = " + request.UpdateContent.Id);

                var vKeepContactId = masterid.Id;
                var vDiscardContactId = contact.Id;

                var loginUserId = context.InitiatingUserId;

                CreateRecordInContactTransform(vKeepContactId, vDiscardContactId, loginUserId);

                tracingService.Trace("vKeepContactId = " + vKeepContactId);
                tracingService.Trace("vDiscardContactId = " + vDiscardContactId);

                tracingService.Trace("Before RemoveDuplicateClientBrand");
                RemoveDuplicateClientBrand(vKeepContactId, vDiscardContactId); //Action 1: -Remove Duplicate "stt_clientbrand" records.
                tracingService.Trace(" afterRemoveDuplicateClientBrand");

                tracingService.Trace("Before SetIsPrimaryNoOnRelatedAddress");
                SetIsPrimaryNoOnRelatedAddress(vKeepContactId, vDiscardContactId);//Action 2a: - Set IsPrimary = No on related "stt_address" records
                tracingService.Trace("after SetIsPrimaryNoOnRelatedAddress");

                tracingService.Trace("before SetIsPrimaryNoOnRelatedAddress");
                RemoveDuplicateAddress(vKeepContactId, vDiscardContactId);//Action 2b: - Remove Duplicate "stt_address" records.
                tracingService.Trace("after SetIsPrimaryNoOnRelatedAddress");

                tracingService.Trace("before SetIsPrimaryOnAddress");
                SetIsPrimaryOnAddress(vKeepContactId, vDiscardContactId);//Action 2c:- Set stt_isprimary=true on "stt_address" records.
                tracingService.Trace("after SetIsPrimaryOnAddress");

                tracingService.Trace("before RemoveDuplicateLicenseNumber");
                RemoveDuplicateLicenseNumber(vKeepContactId, vDiscardContactId);//Action 3: - Remove duplicate License Number records
                tracingService.Trace("after RemoveDuplicateLicenseNumber");

                tracingService.Trace("before RemoveDuplicateClientMSADetails");
                RemoveDuplicateClientMSADetails(vKeepContactId, vDiscardContactId);//Action 4: - Remove Duplicate "stt_clientmsadetails" records.
                tracingService.Trace("after RemoveDuplicateClientMSADetails");

                tracingService.Trace("before RemoveDuplicateClientbranddetails");
                RemoveDuplicateClientbranddetails(vKeepContactId, vDiscardContactId);//Action 5: - Remove Duplicate "stt_clientbranddetails" records.
                tracingService.Trace("after RemoveDuplicateClientbranddetails");

                tracingService.Trace("before RemoveDuplicateClientDetail");
                RemoveDuplicateClientDetail(vKeepContactId, vDiscardContactId);//Action 7 - Remove Duplicate "stt_clientdetails" records
                tracingService.Trace("after RemoveDuplicateClientDetail");

                tracingService.Trace("before SetClientDetailslookupOnRelatedPhonecall");
                SetClientDetailslookupOnRelatedPhonecall(vKeepContactId, vDiscardContactId);//Action 8a - Set ClientDetails lookup on related Phonecall records
                tracingService.Trace("after SetClientDetailslookupOnRelatedPhonecall");

                tracingService.Trace("before SetClientDetailslookupOnRelatedAppointment");
                SetClientDetailslookupOnRelatedAppointment(vKeepContactId, vDiscardContactId);//Action 8b - Set ClientDetails lookup on related Appointment records
                tracingService.Trace("after SetClientDetailslookupOnRelatedAppointment");

                tracingService.Trace("before SetClientDetailslookupOnRelatedTransactionRole");
                SetClientDetailslookupOnRelatedTransactionRole(vKeepContactId, vDiscardContactId);//Action 9 - Set ClientDetails lookup on related stt_transactionrole records
                tracingService.Trace("after SetClientDetailslookupOnRelatedTransactionRole");

                tracingService.Trace("before SetClientMSADetailsOnTransactionMSA");
                SetClientMSADetailsOnTransactionMSA(vKeepContactId, vDiscardContactId);//Action 10a - Set "Client MSA Details" lookup on stt_Transactionmsa records
                tracingService.Trace("after SetClientMSADetailsOnTransactionMSA");

                tracingService.Trace("before SetClientBrandOnTransactionMSA");
                SetClientBrandOnTransactionMSA(vKeepContactId, vDiscardContactId);//Action 10b - Set "Client Brand" lookup on stt_Transactionmsa records
                tracingService.Trace("after SetClientBrandOnTransactionMSA");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("an error occured in plugin ContactPostMerge = " + ex.Message);
            }
        }
        private void CreateRecordInContactTransform(Guid vKeepContactId, Guid vDiscardContactId, Guid loginUserId)
        {
            Entity loginUserEntity = service.Retrieve("systemuser", loginUserId , new ColumnSet("fullname", "internalemailaddress"));
            string fullName = loginUserEntity.GetAttributeValue<string>("fullname");
            string email = loginUserEntity.GetAttributeValue<string>("internalemailaddress");

            Entity entityKeepContact = service.Retrieve("contact", vKeepContactId, new ColumnSet("stt_contactfinanceidtext"));
            Entity entityDiscardContact = service.Retrieve("contact", vDiscardContactId, new ColumnSet("stt_contactfinanceidtext"));

            Entity createTransformRecord = new Entity("stt_contacttransform");

            createTransformRecord["stt_matchingnotes"] = "The merge activity is performed by " + fullName + ", Email is " + email;

            createTransformRecord["stt_mergefromfinanceid"] = entityDiscardContact.Contains("stt_contactfinanceidtext") && entityDiscardContact["stt_contactfinanceidtext"] != null?
               entityDiscardContact.GetAttributeValue<string>("stt_contactfinanceidtext") : null;
            createTransformRecord["stt_mergefromcontactid"] = entityDiscardContact.Id.ToString();

            createTransformRecord["stt_mergetofinanceid"] = entityKeepContact.Contains("stt_contactfinanceidtext") && entityKeepContact["stt_contactfinanceidtext"] != null ?
               entityKeepContact.GetAttributeValue<string>("stt_contactfinanceidtext") : null; ;
            createTransformRecord["stt_mergetocontactid"] = entityKeepContact.Id.ToString();

            service.Create(createTransformRecord);

        }
        private void SetClientBrandOnTransactionMSA(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='stt_transactionmsa'>
                                    <attribute name='stt_transactionmsaid' />
                                    <attribute name='stt_name' />
                                    <attribute name='createdon' />
                                    <attribute name='stt_msaid' />
                                    <order attribute='createdon' descending='true' />
                                    <link-entity name='stt_transaction' from='stt_transactionid' to='stt_transactionid' link-type='inner'>
                                        <attribute name='stt_brandid' alias='TransactionBrand' />                                       
                                       <filter type='and'>
                                        <condition attribute='stt_directableside1id' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                foreach (Entity transactionmsa in collection.Entities)
                {
                    EntityReference TransactionBrand = transactionmsa.Contains("TransactionBrand") && transactionmsa["TransactionBrand"] != null ?
                       (EntityReference)transactionmsa.GetAttributeValue<AliasedValue>("TransactionBrand").Value : null;


                    if (TransactionBrand != null)
                    {
                        string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='stt_clientbrand'>
                                            <attribute name='stt_clientbrandid' />
                                            <attribute name='createdon' />
                                            <attribute name='stt_brandid' />
                                            <attribute name='stt_contactid' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq'  value='0' />
                                              <condition attribute='stt_brandid' operator='eq'  value='" + TransactionBrand.Id + @"' />
                                              <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' /> 
                                            </filter>
   
                                          </entity>
                                        </fetch>";
                        EntityCollection collectionClientDetail = service.RetrieveMultiple(new FetchExpression(fetch));
                        foreach (Entity clientBrand in collectionClientDetail.Entities)
                        {

                            Entity updateTransactionmsa = new Entity(transactionmsa.LogicalName);

                            updateTransactionmsa.Id = transactionmsa.Id;
                            updateTransactionmsa["stt_clientbrandid"] = new EntityReference(clientBrand.LogicalName, clientBrand.Id);
                            service.Update(updateTransactionmsa);

                        }
                    }

                }

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetClientMSADetailsOnTransactionMSA ContactPostMerge Plugin=" + ex.Message);
            }
        }
        private void SetClientMSADetailsOnTransactionMSA(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='stt_transactionmsa'>
                                    <attribute name='stt_transactionmsaid' />
                                    <attribute name='stt_name' />
                                    <attribute name='createdon' />
                                    <attribute name='stt_msaid' />
                                    <order attribute='createdon' descending='true' />
                                    <link-entity name='stt_transaction' from='stt_transactionid' to='stt_transactionid' link-type='inner' >
                                      <filter type='and'>
                                        <condition attribute='stt_directableside1id' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                foreach (Entity transactionmsa in collection.Entities)
                {

                    EntityReference stt_msaid = transactionmsa.Contains("stt_msaid") && transactionmsa["stt_msaid"] != null ?
                        transactionmsa.GetAttributeValue<EntityReference>("stt_msaid") : null;


                    if (stt_msaid != null)
                    {
                        string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='stt_clientmsadetails'>
                                            <attribute name='stt_clientmsadetailsid' />
                                            <attribute name='createdon' />
                                            <attribute name='stt_msaid' />
                                            <attribute name='stt_contactid' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq'  value='0' />
                                              <condition attribute='stt_msaid' operator='eq'  value='" + stt_msaid.Id + @"' />
                                              <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' /> 
                                            </filter>
   
                                          </entity>
                                        </fetch>";
                        EntityCollection collectionClientDetail = service.RetrieveMultiple(new FetchExpression(fetch));
                        foreach (Entity clientMSADetial in collectionClientDetail.Entities)
                        {

                            Entity updateTransactionmsa = new Entity(transactionmsa.LogicalName);

                            updateTransactionmsa.Id = transactionmsa.Id;
                            updateTransactionmsa["stt_clientmsadetailsid"] = new EntityReference(clientMSADetial.LogicalName, clientMSADetial.Id);
                            service.Update(updateTransactionmsa);

                        }
                    }

                }

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetClientMSADetailsOnTransactionMSA ContactPostMerge Plugin=" + ex.Message);
            }
        }
        private void SetClientDetailslookupOnRelatedTransactionRole(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='stt_transactionrole'>
                            <attribute name='stt_transactionroleid' />
                            <attribute name='stt_name' />
                            <attribute name='stt_clientdetailsid' />
                            <attribute name='createdon' />
                            <attribute name='stt_rolecode' />
                            <attribute name='stt_contactid' />
                            <attribute name='stt_transactionid' />
                            <attribute name='stt_isdirectingcient' />
                            <order attribute='stt_name' descending='false' />
                             <filter type='and'>
                                <condition attribute='stt_contactid' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                              </filter>
                            <link-entity name='stt_transaction' from='stt_transactionid' to='stt_transactionid' visible='false' link-type='outer'>
                              <attribute name='stt_marketid' alias='TransactionMarket'/>
                            </link-entity>
                          </entity>
                        </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                foreach (Entity transactionrole in collection.Entities)
                {

                    EntityReference transactionroleContact = transactionrole.Contains("stt_contactid") && transactionrole["stt_contactid"] != null ?
                        transactionrole.GetAttributeValue<EntityReference>("stt_contactid") : null;

                    EntityReference transactionroleClientDetail = transactionrole.Contains("stt_clientdetailsid") && transactionrole["stt_clientdetailsid"] != null ?
                        transactionrole.GetAttributeValue<EntityReference>("stt_clientdetailsid") : null;

                    EntityReference TransactionMarket = transactionrole.Contains("TransactionMarket") && transactionrole["TransactionMarket"] != null ?
                        (EntityReference)transactionrole.GetAttributeValue<AliasedValue>("TransactionMarket").Value : null;

                    if (transactionroleContact != null && TransactionMarket != null)
                    {
                        string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='stt_clientdetails'>
                                            <attribute name='stt_clientdetailsid' />
                                            <attribute name='createdon' />
                                            <attribute name='stt_clientstagecode' />
                                            <attribute name='stt_marketid' />
                                            <attribute name='stt_bdoid' />
                                            <attribute name='stt_isaid' />
                                            <attribute name='stt_businessstrategistid' />
                                            <attribute name='stt_contactid' />
                                            <order attribute='stt_clientstagecode' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='stt_marketid' operator='eq' uitype='stt_market' value='" + TransactionMarket.Id + @"' />
                                              <condition attribute='stt_contactid' operator='eq' uitype='contact' value='" + transactionroleContact.Id + @"' />
                                            </filter>
   
                                          </entity>
                                        </fetch>";
                        EntityCollection collectionClientDetail = service.RetrieveMultiple(new FetchExpression(fetch));
                        foreach (Entity clientDetial in collectionClientDetail.Entities)
                        {
                            if (transactionroleClientDetail != null && transactionroleClientDetail.Id != clientDetial.Id)
                            {
                                Entity updateTransactionRole = new Entity(transactionrole.LogicalName);

                                updateTransactionRole.Id = transactionrole.Id;
                                updateTransactionRole["stt_clientdetailsid"] = new EntityReference(clientDetial.LogicalName, clientDetial.Id);
                                service.Update(updateTransactionRole);
                            }
                        }
                    }

                }

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetClientDetailslookupOnRelatedTransactionRole ContactPostMerge Plugin=" + ex.Message);
            }
        }
        private void SetClientDetailslookupOnRelatedAppointment(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch version='1.0'>
                                  <entity name='appointment'>
                                    <attribute name='activityid' />
                                    <attribute name='stt_marketid' />
                                    <attribute name='regardingobjectid' />
                                    <attribute name='stt_clientdetailid' />
                                    <order attribute='subject' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='stt_marketid' operator='not-null' />
                                      <condition attribute='regardingobjectid' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                                    </filter>
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                foreach (Entity appointment in collection.Entities)
                {

                    EntityReference phoneCallClientDetail = appointment.Contains("stt_clientdetailid") && appointment["stt_clientdetailid"] != null ?
                        appointment.GetAttributeValue<EntityReference>("stt_clientdetailid") : null;

                    EntityReference regardingobjectid = appointment.Contains("regardingobjectid") && appointment["regardingobjectid"] != null ?
                        appointment.GetAttributeValue<EntityReference>("regardingobjectid") : null;

                    EntityReference stt_marketid = appointment.Contains("stt_marketid") && appointment["stt_marketid"] != null ?
                        appointment.GetAttributeValue<EntityReference>("stt_marketid") : null;

                    if (regardingobjectid != null && stt_marketid != null)
                    {
                        string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='stt_clientdetails'>
                                            <attribute name='stt_clientdetailsid' />
                                            <attribute name='createdon' />
                                            <attribute name='stt_clientstagecode' />
                                            <attribute name='stt_marketid' />
                                            <attribute name='stt_bdoid' />
                                            <attribute name='stt_isaid' />
                                            <attribute name='stt_businessstrategistid' />
                                            <attribute name='stt_contactid' />
                                            <order attribute='stt_clientstagecode' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='stt_marketid' operator='eq' uitype='stt_market' value='" + stt_marketid.Id + @"' />
                                              <condition attribute='stt_contactid' operator='eq' uitype='contact' value='" + regardingobjectid.Id + @"' />
                                            </filter>
   
                                          </entity>
                                        </fetch>";
                        EntityCollection collectionClientDetail = service.RetrieveMultiple(new FetchExpression(fetch));
                        foreach (Entity clientDetial in collectionClientDetail.Entities)
                        {
                            if (phoneCallClientDetail != null && phoneCallClientDetail.Id != clientDetial.Id)
                            {
                                Entity updatePhoneCall = new Entity(appointment.LogicalName);

                                updatePhoneCall.Id = appointment.Id;
                                updatePhoneCall["stt_clientdetailid"] = new EntityReference(clientDetial.LogicalName, clientDetial.Id);
                                service.Update(updatePhoneCall);
                            }
                        }
                    }

                }

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetClientDetailslookupOnRelatedAppointment = " + ex.Message);
            }
        }
        private void SetClientDetailslookupOnRelatedPhonecall(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch version='1.0'>
                                  <entity name='phonecall'>
                                    <attribute name='subject' />
                                    <attribute name='prioritycode' />
                                    <attribute name='scheduledend' />
                                    <attribute name='regardingobjectid' />
                                    <attribute name='activityid' />
                                    <attribute name='stt_marketid' />
                                    <attribute name='stt_clientdetailid' />
                                    <order attribute='subject' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='stt_marketid' operator='not-null' />
                                      <condition attribute='regardingobjectid' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                                    </filter>
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                foreach (Entity phoneCall in collection.Entities)
                {

                    EntityReference phoneCallClientDetail = phoneCall.Contains("stt_clientdetailid") && phoneCall["stt_clientdetailid"] != null ?
                        phoneCall.GetAttributeValue<EntityReference>("stt_clientdetailid") : null;

                    EntityReference regardingobjectid = phoneCall.Contains("regardingobjectid") && phoneCall["regardingobjectid"] != null ?
                        phoneCall.GetAttributeValue<EntityReference>("regardingobjectid") : null;

                    EntityReference stt_marketid = phoneCall.Contains("stt_marketid") && phoneCall["stt_marketid"] != null ?
                        phoneCall.GetAttributeValue<EntityReference>("stt_marketid") : null;

                    tracingService.Trace("before regardingobjectid != null && stt_marketid != null");
                    if (regardingobjectid != null && stt_marketid != null)
                    {
                        tracingService.Trace("inside regardingobjectid != null && stt_marketid != null");
                        string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='stt_clientdetails'>
                                            <attribute name='stt_clientdetailsid' />
                                            <attribute name='createdon' />
                                            <attribute name='stt_clientstagecode' />
                                            <attribute name='stt_marketid' />
                                            <attribute name='stt_bdoid' />
                                            <attribute name='stt_isaid' />
                                            <attribute name='stt_businessstrategistid' />
                                            <attribute name='stt_contactid' />
                                            <order attribute='stt_clientstagecode' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='stt_marketid' operator='eq' uitype='stt_market' value='" + stt_marketid.Id + @"' />
                                              <condition attribute='stt_contactid' operator='eq' uitype='contact' value='" + regardingobjectid.Id + @"' />
                                            </filter>
                                          </entity>
                                        </fetch>";
                        EntityCollection collectionClientDetail = service.RetrieveMultiple(new FetchExpression(fetch));

                        foreach (Entity clientDetial in collectionClientDetail.Entities)
                        {
                            if (phoneCallClientDetail != null && phoneCallClientDetail.Id != clientDetial.Id)
                            {
                                Entity updatePhoneCall = new Entity(phoneCall.LogicalName);

                                updatePhoneCall.Id = phoneCall.Id;
                                updatePhoneCall["stt_clientdetailid"] = new EntityReference(clientDetial.LogicalName, clientDetial.Id);
                                service.Update(updatePhoneCall);
                            }
                        }
                    }

                }

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetClientDetailslookupOnRelatedAppointment = " + ex.Message);
            }
        }
        private void RemoveDuplicateClientDetail(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch>
                                  <entity name='stt_clientdetails'>
                                    <attribute name='stt_name' />
                                    <attribute name='stt_marketid' />
                                    <attribute name='stt_contactid' />
                                    <attribute name='modifiedon' />
                                    <filter>
                                      <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' />
                                      <condition attribute='statecode' operator='eq' value='0' />
                                    </filter>
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                FindDuplicatesClientBrandDetailAndClientDetail(collection, vKeepContactId, vDiscardContactId);
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function RemoveDuplicateClientDetail plugin ContactPostMerge=" + ex.Message);
            }
        }
        private void RemoveDuplicateClientbranddetails(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                string fetchXml = @"<fetch>
                              <entity name='stt_clientbranddetails'>
                                <attribute name='stt_name' />
                                <attribute name='stt_marketid' />
                                <attribute name='stt_contactid' />
                                <attribute name='modifiedon' />
                                <filter>
                                  <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' />
                                  <condition attribute='statecode' operator='eq' value='0' />
                                </filter>
                              </entity>
                            </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                FindDuplicatesClientBrandDetailAndClientDetail(collection, vKeepContactId, vDiscardContactId);
            }
            catch (Exception ex)
            {

                tracingService.Trace("an error occured in function RemoveDuplicateClientbranddetails plugin ContactPostMerge=" + ex.Message);
            }
        }
        private void RemoveDuplicateClientMSADetails(Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside RemoveDuplicateClientMSADetails");
            try
            {
                string fetchXml = @"<fetch>
                          <entity name='stt_clientmsadetails'>
                            <attribute name='stt_clientmsadetailsid' />
                            <attribute name='stt_name' />
                            <attribute name='stt_msaid' />
                            <attribute name='stt_contactid' />
                            <attribute name='modifiedon' />
                            <filter>
                              <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' />
                              <condition attribute='statecode' operator='eq' value='0' />
                            </filter>
                          </entity>
                        </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                FindDuplicatesClientMSADetail(collection, vKeepContactId, vDiscardContactId);
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("an error occured in function RemoveDuplicateClientMSADetails Plugin ContactPostMerge=" + ex.Message);
            }
        }
        private void RemoveDuplicateLicenseNumber(Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside RemoveDuplicateLicenseNumber");
            try
            {
                string fetchXml = @"<fetch>
                                  <entity name='stt_licensenumber'>
                                    <attribute name='stt_licensenumber' />
                                    <attribute name='stt_licensenumberid' />
                                    <attribute name='stt_typecode' />
                                    <attribute name='stt_stateid' />
                                    <attribute name='stt_contactid' />
                                    <attribute name='modifiedon' />
                                    <filter>
                                      <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' />
                                      <condition attribute='statecode' operator='eq' value='0' />
                                    </filter>
                                    <order attribute='stt_licensenumber' />
                                  </entity>
                                </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                FindDuplicatesLicenseNumber(collection, vKeepContactId, vDiscardContactId);


            }
            catch (Exception ex)
            {
                tracingService.Trace("an error occured in function RemoveDuplicateLicenseNumber plugin ContactPostMerge=" + ex.Message);
            }
        }
        private void SetIsPrimaryOnAddress(Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside SetIsPrimaryOnAddress");
            try
            {
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='stt_address'>
                                <attribute name='stt_addressid' />
                                <attribute name='stt_address' />
                                <attribute name='createdon' />
                                <attribute name='stt_addresstypecode' />
                                <attribute name='stt_contactid' />
                                <attribute name='stt_brandid' />
                                <attribute name='stt_marketid' />
                                <attribute name='stt_emailaddress' />
                                <attribute name='stt_address1' />
                                <attribute name='stt_address2' />
                                <attribute name='stt_city' />
                                <attribute name='createdby' />
                                <attribute name='stt_stateid' />
                                <attribute name='stt_zipcodeid' />
                                <attribute name='stt_countyid' />
                                <attribute name='stt_postalcode' />
                                <attribute name='stt_telephone1' />
                                <attribute name='stt_isprimary' />
                                <order attribute='createdon' descending='true' />
                                <filter type='and'>
                                  <condition attribute='stt_contactid' operator='eq' uiname='0819 0152123' uitype='contact' value='" + vKeepContactId + @"' />
                                  <condition attribute='statecode' operator='eq' value='0' />
                                </filter>
                                <link-entity name='contact' from='contactid' to='stt_contactid' visible='false' link-type='outer'>
                                  <attribute name='mobilephone' alias='ContactMobilePhone'/>
                                  <attribute name='emailaddress1' alias='ContactEmailaddress1'/>
                                  <attribute name='telephone1' alias='ContactTelephone1'/>
                                  <attribute name='address1_composite' alias='ContactAddressComposite'/>
                                  <attribute name='address1_line1' alias='ContactAddress1_line1'/>
                                  <attribute name='address1_line2' alias='ContactAddress1_line2'/>
                                  <attribute name='address1_city' alias='ContactAddress1_city'/>
                                  <attribute name='stt_stateid' alias='Contactstt_stateid'/>
                                  <attribute name='stt_countyid' alias='Contactstt_countyid'/>
                                  <attribute name='stt_zipcodeid' alias='Contactstt_zipcodeid'/>
                                </link-entity>
                              </entity>
                            </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));

                foreach (Entity entity in collection.Entities)
                {
                    int addresstypecode = entity.Contains("stt_addresstypecode") && entity["stt_addresstypecode"] != null ?
                        entity.GetAttributeValue<OptionSetValue>("stt_addresstypecode").Value : 0;

                    string addressTelephone = entity.Contains("stt_telephone1") && entity["stt_telephone1"] != null ?
                        entity.GetAttributeValue<string>("stt_telephone1") : string.Empty;

                    string ContactTelephone1 = entity.Contains("ContactTelephone1") && entity["ContactTelephone1"] != null ?
                        (string)entity.GetAttributeValue<AliasedValue>("ContactTelephone1").Value : string.Empty;

                    string ContactMobilePhone = entity.Contains("ContactMobilePhone") && entity["ContactMobilePhone"] != null ?
                        (string)entity.GetAttributeValue<AliasedValue>("ContactMobilePhone").Value : string.Empty;

                    string addressEmailAddress = entity.Contains("stt_emailaddress") && entity["stt_emailaddress"] != null ?
                      entity.GetAttributeValue<string>("stt_emailaddress") : string.Empty;

                   // tracingService.Trace("addressEmailAddress = " + addressEmailAddress);

                    string ContactEmailaddress1 = entity.Contains("ContactEmailaddress1") && entity["ContactEmailaddress1"] != null ?
                        (string)entity.GetAttributeValue<AliasedValue>("ContactEmailaddress1").Value : string.Empty;
                  //  tracingService.Trace("ContactEmailaddress1 = " + ContactEmailaddress1);

                    string stt_address = entity.Contains("stt_address") && entity["stt_address"] != null ?
                     entity.GetAttributeValue<string>("stt_address") : string.Empty;
                  //  tracingService.Trace("stt_address = "+ stt_address);

                    string ContactAddressComposite = entity.Contains("ContactAddressComposite") && entity["ContactAddressComposite"] != null ?
                        (string)entity.GetAttributeValue<AliasedValue>("ContactAddressComposite").Value : string.Empty;

                  //  tracingService.Trace("ContactAddressComposite = " + ContactAddressComposite);

                    //------------------------------------------------------------------------------------
                    string stt_address1 = entity.Contains("stt_address1") && entity["stt_address1"] != null ?
                     entity.GetAttributeValue<string>("stt_address1") : string.Empty;
                   // tracingService.Trace("stt_address1 = " + stt_address1);

                    string stt_address2 = entity.Contains("stt_address2") && entity["stt_address2"] != null ?
                     entity.GetAttributeValue<string>("stt_address2") : string.Empty;
                   // tracingService.Trace("stt_address2 = " + stt_address2);

                    string stt_city = entity.Contains("stt_city") && entity["stt_city"] != null ?
                     entity.GetAttributeValue<string>("stt_city") : string.Empty;
                   // tracingService.Trace("stt_city = " + stt_city);

                    EntityReference stt_stateid = entity.Contains("stt_stateid") && entity["stt_stateid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_stateid") : null;
                    Guid StateId = Guid.Empty;
                    if(stt_stateid != null)
                    {
                        StateId = stt_stateid.Id;
                    }

                    EntityReference stt_countyid = entity.Contains("stt_countyid") && entity["stt_countyid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_countyid") : null;
                 

                    EntityReference stt_zipcodeid = entity.Contains("stt_zipcodeid") && entity["stt_zipcodeid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_zipcodeid") : null;
                    Guid ZipCodeId = Guid.Empty;
                    if (stt_zipcodeid != null)
                    {
                        ZipCodeId = stt_zipcodeid.Id;
                    }



                    //------------------------------------------------------------------------------------

                    string ContactAddress1_line1 = entity.Contains("ContactAddress1_line1") && entity["ContactAddress1_line1"] != null ?
                    (string)entity.GetAttributeValue<AliasedValue>("ContactAddress1_line1").Value : string.Empty;
                  //  tracingService.Trace("ContactAddress1_line1 = " + ContactAddress1_line1);

                    string ContactAddress1_line2 = entity.Contains("ContactAddress1_line2") && entity["ContactAddress1_line2"] != null ?
                     (string)entity.GetAttributeValue<AliasedValue>("ContactAddress1_line2").Value : string.Empty;
                   // tracingService.Trace("ContactAddress1_line2 = " + ContactAddress1_line2);

                    string ContactAddress1_city = entity.Contains("ContactAddress1_city") && entity["ContactAddress1_city"] != null ?
                     (string)entity.GetAttributeValue<AliasedValue>("ContactAddress1_city").Value : string.Empty;
                   // tracingService.Trace("ContactAddress1_city = " + ContactAddress1_city);

                    EntityReference Contactstt_stateid = entity.Contains("Contactstt_stateid") && entity["Contactstt_stateid"] != null ?
                     (EntityReference)entity.GetAttributeValue<AliasedValue>("Contactstt_stateid").Value : null;
                    //tracingService.Trace("Contactstt_stateid = " + Contactstt_stateid);

                    Guid ContactStateId = Guid.Empty;
                    if (Contactstt_stateid != null)
                    {
                        ContactStateId = Contactstt_stateid.Id;
                    }

                    EntityReference Contactstt_countyid = entity.Contains("Contactstt_countyid") && entity["Contactstt_countyid"] != null ?
                     (EntityReference)entity.GetAttributeValue<AliasedValue>("Contactstt_countyid").Value : null;
                    //tracingService.Trace("Contactstt_countyid = " + Contactstt_countyid);

                    EntityReference Contactstt_zipcodeid = entity.Contains("Contactstt_zipcodeid") && entity["Contactstt_zipcodeid"] != null ?
                     (EntityReference)entity.GetAttributeValue<AliasedValue>("Contactstt_zipcodeid").Value : null;

                    Guid ContactZipCodeId = Guid.Empty;
                    if (Contactstt_zipcodeid != null)
                    {
                        ContactZipCodeId = Contactstt_zipcodeid.Id;
                    }
                    // tracingService.Trace("Contactstt_zipcodeid = " + Contactstt_zipcodeid);

                    //-------------------------------------------------------------------------------------

                    if (addresstypecode == 924510002 && addressTelephone.Trim().Equals(ContactTelephone1.Trim())) //924510002 : Phone
                    {
                        tracingService.Trace("924510002 : Phone ");
                        UpdateRecord(entity, "stt_isprimary", vKeepContactId, vDiscardContactId, "bool", "true");
                    }
                    else if (addresstypecode == 924510003 && addressTelephone.Trim().Equals(ContactMobilePhone.Trim())) //924510003 : Mobile
                    {
                        tracingService.Trace("924510003 : Mobile");
                        UpdateRecord(entity, "stt_isprimary", vKeepContactId, vDiscardContactId, "bool", "true");

                    }
                    else if (addresstypecode == 924510000 && addressEmailAddress.Trim().Equals(ContactEmailaddress1.Trim())) //924510000 : Email
                    {
                        tracingService.Trace("924510000 : Email");
                        UpdateRecord(entity, "stt_isprimary", vKeepContactId, vDiscardContactId, "bool", "true");
                    }
                    else if (addresstypecode == 924510001 && stt_address1.Trim().Equals(ContactAddress1_line1.Trim())
                        && stt_address2.Trim().Equals(ContactAddress1_line2.Trim()) && stt_city.Trim().Equals(ContactAddress1_city.Trim())
                        && StateId.Equals(ContactStateId) && ZipCodeId.Equals(ContactZipCodeId)) //924510001 : Address
                    {
                        tracingService.Trace("Inside SetIsPrimaryOnAddress 924510001 : Address");
                        UpdateRecord(entity, "stt_isprimary", vKeepContactId, vDiscardContactId, "bool", "true");
                    }
                    
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("error in function SetIsPrimaryOnAddress plugin ContactPostMerge" + ex.Message);
            }
        }
        private void RemoveDuplicateClientBrand(Guid vKeepContactId, Guid vDiscardContactId)
        {
            try
            {
                tracingService.Trace("inside RemoveDuplicateClientBrand");
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='stt_clientbrand'>
                                <attribute name='stt_clientbrandid' />
                                <attribute name='stt_name' />
                                <attribute name='createdon' />
                                <attribute name='stt_contactid' />
                                <attribute name='stt_brandid' />
                                <attribute name='modifiedon' />
                                <order attribute='stt_brandid' descending='false' />
                                <order attribute='modifiedon' descending='true' />
                                <filter type='and'>
                                  <condition attribute='stt_contactid' operator='eq' uitype='contact' value='" + vKeepContactId + @"' />
                                  <condition attribute='statecode' operator='eq' value='0' />
                                </filter>
                              </entity>
                            </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                tracingService.Trace("stt_clientbrand collection.Entities.Count = " + collection.Entities.Count);
                if (collection.Entities.Count > 0)
                {
                    FindDuplicates(collection, vKeepContactId, vDiscardContactId);
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("an error occured in plugin ContactPostMerge function RemoveDuplicateClientBrand = " + ex.Message);
            }
        }
        private void SetIsPrimaryNoOnRelatedAddress(Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside SetIsPrimaryNoOnRelatedAddress");
            try
            {
                string fetchXml = @"<fetch>
                              <entity name='stt_address'>
                                <attribute name='stt_addressid' />
                                <attribute name='stt_isprimary' />
                                <attribute name='stt_contactid' />
                                <attribute name='statecode' />
                                <filter>
                                  <condition attribute='stt_contactid' operator='eq' value='" + vKeepContactId + @"' />
                                  <condition attribute='stt_isprimary' operator='eq' value='1' />
                                  <condition attribute='statecode' operator='eq' value='0' />
                                </filter>
                              </entity>
                            </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                tracingService.Trace("collection.Entities.Count=" + collection.Entities.Count);
                foreach (Entity entity in collection.Entities)
                {
                    Entity update = new Entity(entity.LogicalName);
                    update["stt_isprimary"] = false;
                    update.Id = entity.Id;
                    service.Update(update);

                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("an error occured in function SetIsPrimaryNoOnRelatedAddress,plugin PostContactMerge=" + ex.Message);
            }
        }
        private void RemoveDuplicateAddress(Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside RemoveDuplicateAddress");
            try
            {
                string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='stt_address'>
                                <attribute name='stt_addressid' />
                                <attribute name='stt_address' />
                                <attribute name='createdon' />
                                <attribute name='stt_addresstypecode' />
                                <attribute name='stt_contactid' />
                                <attribute name='stt_brandid' />
                                <attribute name='stt_marketid' />
                                <attribute name='stt_emailaddress' />
                                <attribute name='stt_address1' />
                                <attribute name='stt_address2' />
                                <attribute name='stt_city' />
                                <attribute name='createdby' />
                                <attribute name='stt_stateid' />
                                <attribute name='stt_zipcodeid' />
                                <attribute name='stt_countyid' />
                                <attribute name='stt_postalcode' />
                                <attribute name='stt_telephone1' />
                                <attribute name='stt_isprimary' />
                                <order attribute='stt_addresstypecode' descending='true' />
                                <filter type='and'>
                                  <condition attribute='stt_contactid' operator='eq'  uitype='contact' value='" + vKeepContactId + @"' />
                                  <condition attribute='statecode' operator='eq' value='0' />
                                </filter>
                              </entity>
                            </fetch>";
                EntityCollection collection = service.RetrieveMultiple(new FetchExpression(fetchXml));
                string addressTelephoneGlobal = string.Empty;
                string addressEmailAddressGlobal = string.Empty;
                string stt_address1Global= string.Empty;
                string stt_address2Global= string.Empty;
                string stt_cityGlobal = string.Empty;
                Guid stt_stateidGlobal  = Guid.Empty;
                Guid stt_zipcodeidGlobal = Guid.Empty;

                foreach (Entity entity in collection.Entities)
                {
                    int addresstypecode = entity.Contains("stt_addresstypecode") && entity["stt_addresstypecode"] != null ?
                        entity.GetAttributeValue<OptionSetValue>("stt_addresstypecode").Value : 0;
                    tracingService.Trace("addresstypecode = "+ addresstypecode);
                    string addressTelephone = entity.Contains("stt_telephone1") && entity["stt_telephone1"] != null ?
                        entity.GetAttributeValue<string>("stt_telephone1") : string.Empty;

                    string addressEmailAddress = entity.Contains("stt_emailaddress") && entity["stt_emailaddress"] != null ?
                        entity.GetAttributeValue<string>("stt_emailaddress") : string.Empty;

                    string stt_address1 = entity.Contains("stt_address1") && entity["stt_address1"] != null ?
                     entity.GetAttributeValue<string>("stt_address1") : string.Empty;
            

                    string stt_address2 = entity.Contains("stt_address2") && entity["stt_address2"] != null ?
                     entity.GetAttributeValue<string>("stt_address2") : string.Empty;
          

                    string stt_city = entity.Contains("stt_city") && entity["stt_city"] != null ?
                     entity.GetAttributeValue<string>("stt_city") : string.Empty;
                    

                    EntityReference stt_stateid = entity.Contains("stt_stateid") && entity["stt_stateid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_stateid") : null;

                    Guid StateId = Guid.Empty;
                    if (stt_stateid != null)
                    {
                        StateId = stt_stateid.Id;
                    }


                    EntityReference stt_countyid = entity.Contains("stt_countyid") && entity["stt_countyid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_countyid") : null;


                    EntityReference stt_zipcodeid = entity.Contains("stt_zipcodeid") && entity["stt_zipcodeid"] != null ?
                     entity.GetAttributeValue<EntityReference>("stt_zipcodeid") : null;

                    Guid ZipCodeId = Guid.Empty;
                    if (stt_zipcodeid != null)
                    {
                        ZipCodeId = stt_zipcodeid.Id;
                    }

                    tracingService.Trace("addressTelephone.Trim() ="+ addressTelephone.Trim());
                    tracingService.Trace("addressTelephoneGlobal.Trim() =" + addressTelephoneGlobal.Trim());
                    if ((addresstypecode == 924510002 || addresstypecode == 924510003) && addressTelephone != string.Empty && addressTelephoneGlobal != string.Empty
                        && addressTelephone.Trim().Equals(addressTelephoneGlobal.Trim())) //924510002 : Phone
                    {
                        tracingService.Trace("addresstypecode="+ addresstypecode);
                        tracingService.Trace("inside RemoveDuplicateAddress 924510002 : Phone ");
                        string attributeName = "stt_contactid";
                        string attributeType = "lookup";
                        UpdateRecord(entity, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                        tracingService.Trace($"Deactivating: {entity.Id}");

                        DecativateRecord(entity);

                    }
                    else if (addresstypecode == 924510000 && addressEmailAddress != string.Empty && addressEmailAddressGlobal != string.Empty 
                        && addressEmailAddress.Trim().Equals(addressEmailAddressGlobal.Trim())) //924510000 : Email
                    {
                        tracingService.Trace("inside RemoveDuplicateAddress 924510000 : Email");
                        string attributeName = "stt_contactid";
                        string attributeType = "lookup";
                        UpdateRecord(entity, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                        tracingService.Trace($"Deactivating: {entity.Id}");

                        DecativateRecord(entity);

                        
                    }
                    else if (addresstypecode == 924510001 && stt_address1.Trim().Equals(stt_address1Global.Trim())
                        && stt_address2.Trim().Equals(stt_address2Global.Trim()) && stt_city.Trim().Equals(stt_cityGlobal.Trim())
                        && StateId.Equals(stt_stateidGlobal) && ZipCodeId.Equals(stt_zipcodeidGlobal)) //924510001 : Address
                    {
         

                        tracingService.Trace("inside RemoveDuplicateAddress 924510001 : Address");
                        string attributeName = "stt_contactid";
                        string attributeType = "lookup";
                        UpdateRecord(entity, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                        tracingService.Trace($"Deactivating: {entity.Id}");

                        DecativateRecord(entity);

                    }
                    addressTelephoneGlobal = addressTelephone.Trim();
                    addressEmailAddressGlobal = addressEmailAddress.Trim();
                    stt_address1Global = stt_address1.Trim();
                    stt_address2Global = stt_address2.Trim();
                    stt_cityGlobal = stt_city.Trim();
                    stt_stateidGlobal = StateId;
                    stt_zipcodeidGlobal = ZipCodeId;
                }

              //  FindDuplicatesAddress(collection, vKeepContactId, vDiscardContactId);
            }
            catch (Exception ex)
            {
                tracingService.Trace("an error occured in function RemoveDuplicateAddress plugin PostContactMerge=" + ex.Message);
            }
        }
        private void FindDuplicates(EntityCollection collection, Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside FindDuplicates");
            try
            {
                var duplicates = collection.Entities
                    .Where(e => e.Contains("stt_clientbrandid"))
                    .GroupBy(e => e.GetAttributeValue<Guid>("stt_clientbrandid"))
                    .Where(g => g.Count() > 1);

                tracingService.Trace("duplicates.Count=" + duplicates.Count());

                foreach (var group in duplicates)
                {
                    var keepRecord = group.First();
                    var deactivateList = group.Skip(1).ToList();
                    tracingService.Trace($"deactivateList.Count: {deactivateList.Count}");
                    foreach (var record in deactivateList)
                    {
                        string attributeName = "stt_contactid";
                        string attributeType = "lookup";
                        UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType);//Update client brand
                        tracingService.Trace($"Deactivating: {record.Id}");

                        DecativateRecord(record);
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("an error occurred in function FindDuplicates , PostContactMerge plugin =" + ex.Message);
            }
        }
        private void FindDuplicatesAddress(EntityCollection collection, Guid vKeepContactId, Guid vDiscardContactId)
        {
           

            var duplicatesAddress = collection.Entities
                            .Where(e => e.Contains("stt_addresstypecode") && e.GetAttributeValue<OptionSetValue>("stt_addresstypecode") != null)
                            .GroupBy(e => e.GetAttributeValue<OptionSetValue>("stt_addresstypecode").Value)
                            .Where(g => g.Count() > 1 && g.Key == 924510001).SelectMany(g => g).ToList();//924510001 : Address
            tracingService.Trace("duplicatesAddress.Count=" + duplicatesAddress.Count);

            var filterDuplicatesAddress = duplicatesAddress
                                        .GroupBy(e => new
                                        {
                                            stt_address1 = (e.GetAttributeValue<string>("stt_address1") ?? string.Empty).Trim().ToLower(),
                                            stt_address2 = (e.GetAttributeValue<string>("stt_address2") ?? string.Empty).Trim().ToLower(),
                                            stt_city = (e.GetAttributeValue<string>("stt_city") ?? string.Empty).Trim().ToLower(),
                                            stt_stateid = e.GetAttributeValue<EntityReference>("stt_stateid")?.Id ?? Guid.Empty,
                                            stt_zipcodeid = e.GetAttributeValue<EntityReference>("stt_zipcodeid")?.Id ?? Guid.Empty
                                        }).Where(g => g.Count() > 1).SelectMany(g => g).ToList();
            tracingService.Trace("filterDuplicatesAddress.Count=" + filterDuplicatesAddress.Count());

            var duplicateOther = collection.Entities
                            .Where(e => e.Contains("stt_addresstypecode") && e.GetAttributeValue<OptionSetValue>("stt_addresstypecode") != null)
                            .GroupBy(e => e.GetAttributeValue<OptionSetValue>("stt_addresstypecode").Value)
                            .Where(g => g.Count() > 1 && g.Key != 924510001).SelectMany(g => g).ToList(); // 924510001 : Address

            tracingService.Trace("duplicateOther.Count=" + duplicateOther.Count());

            

            //foreach (var group in filterDuplicatesAddress)
            //{
                
              //  var keepRecord = group;
                var deactivateList = filterDuplicatesAddress.Skip(1).ToList();
                tracingService.Trace($"deactivateListfilterDuplicatesAddress.Count: {deactivateList.Count}");
                foreach (var record in deactivateList)
                {
                    string attributeName = "stt_contactid";
                    string attributeType = "lookup";
                    UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                    tracingService.Trace($"Deactivating: {record.Id}");

                    DecativateRecord(record);
                }
           // }

            //foreach (var group in duplicateOther)
            //{
                
               // var keepRecord = group.First();
                var deactivateListOther = duplicateOther.Skip(1).ToList();
                tracingService.Trace($"deactivateListOther.Count: {deactivateListOther.Count}");
                foreach (var record in deactivateList)
                {
                    string attributeName = "stt_contactid";
                    string attributeType = "lookup";
                    UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                    tracingService.Trace($"Deactivating: {record.Id}");

                    DecativateRecord(record);
                }
           // }
        }
        private void FindDuplicatesLicenseNumber(EntityCollection collection, Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside FindDuplicates");
            var duplicates = collection.Entities
                .Where(e => e.Contains("stt_licensenumber") && e.Contains("stt_stateid") && e.GetAttributeValue<string>("stt_licensenumber") != null
                && e.GetAttributeValue<EntityReference>("stt_stateid") != null)
                 .GroupBy(e => new
                 {
                     licensenumber = e.GetAttributeValue<string>("stt_licensenumber"),
                     StatId = e.GetAttributeValue<EntityReference>("stt_stateid").Id
                     // StatId = e.GetAttributeValue<EntityReference>("stt_stateid")?.Id ?? null
                 })
                .Where(g => g.Count() > 1);

            tracingService.Trace("Address duplicates.Count=" + duplicates.Count());

            foreach (var group in duplicates)
            {
                var keepRecord = group.First();
                var deactivateList = group.Skip(1).ToList();
                tracingService.Trace($"deactivateList.Count: {deactivateList.Count}");
                foreach (var record in deactivateList)
                {
                    string attributeName = "stt_contactid";
                    string attributeType = "lookup";
                    UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                    tracingService.Trace($"Deactivating: {record.Id}");

                    DecativateRecord(record);
                }
            }
        }
        private void FindDuplicatesClientMSADetail(EntityCollection collection, Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside FindDuplicatesClientMSADetail");
            var duplicates = collection.Entities
                .Where(e => e.Contains("stt_msaid") && e.GetAttributeValue<EntityReference>("stt_msaid") != null)
                 .GroupBy(e => new
                 {
                     MSAId = e.GetAttributeValue<EntityReference>("stt_msaid").Id
                     // MSAId = e.GetAttributeValue<EntityReference>("stt_msaid")?.Id ?? null
                 })
                .Where(g => g.Count() > 1);

            tracingService.Trace("Address duplicates.Count=" + duplicates.Count());

            foreach (var group in duplicates)
            {
                var keepRecord = group.First();
                var deactivateList = group.Skip(1).ToList();
                // Console.WriteLine($"Duplicate ClientBrand: {group.ClientBrand}");
                foreach (var record in deactivateList)
                {
                    string attributeName = "stt_contactid";
                    string attributeType = "lookup";
                    UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                    tracingService.Trace($"Deactivating: {record.Id}");

                    DecativateRecord(record);
                }
            }
        }
        private void FindDuplicatesClientBrandDetailAndClientDetail(EntityCollection collection, Guid vKeepContactId, Guid vDiscardContactId)
        {
            tracingService.Trace("inside FindDuplicatesClientBrandDetailAndClientDetail");
            var duplicates = collection.Entities
                .Where(e => e.Contains("stt_marketid") && e.GetAttributeValue<EntityReference>("stt_marketid") != null)
                 .GroupBy(e => new
                 {
                     MarketId = e.GetAttributeValue<EntityReference>("stt_marketid").Id
                     // MarketId = e.GetAttributeValue<EntityReference>("stt_marketid")?.Id ?? null
                 })
                .Where(g => g.Count() > 1);

            tracingService.Trace("Address duplicates.Count=" + duplicates.Count());

            foreach (var group in duplicates)
            {
                var keepRecord = group.First();
                var deactivateList = group.Skip(1).ToList();
                tracingService.Trace($"deactivateList.Count: {deactivateList.Count}");
                foreach (var record in deactivateList)
                {
                    string attributeName = "stt_contactid";
                    string attributeType = "lookup";
                    UpdateRecord(record, attributeName, vKeepContactId, vDiscardContactId, attributeType); //Update Address
                    tracingService.Trace($"Deactivating: {record.Id}");

                    DecativateRecord(record);
                }
            }
        }
        private void DecativateRecord(Entity record)
        {
            tracingService.Trace("inside DecativateRecord");
            var setState = new OrganizationRequest("SetState")
            {
                ["EntityMoniker"] = new EntityReference(record.LogicalName, record.Id),
                ["State"] = new OptionSetValue(1),  // 1 = Inactive
                ["Status"] = new OptionSetValue(2) // 2 = Inactive Status
            };

            try
            {
                service.Execute(setState);
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Failed to deactivate {record.Id}: {ex.Message}");
            }
        }
        private void UpdateRecord(Entity record, string attributeName, Guid vKeepContactId, Guid vDiscardContactId, string attributeType, string attributeValue = null)
        {
            tracingService.Trace("inside UpdateRecord");
            try
            {
                Entity update = new Entity(record.LogicalName);
                update.Id = record.Id;
                if (attributeType.Equals("lookup"))
                {
                    update[attributeName] = new EntityReference("contact", vDiscardContactId);
                }
                else if (attributeType.Equals("bool"))
                {
                    update[attributeName] = true;
                }
                service.Update(update);
            }
            catch (Exception ex)
            {
                tracingService.Trace("an error occured in function UpdateRecord, Plugin PostContactMerge=" + ex.Message);
            }
        }
    }
}
