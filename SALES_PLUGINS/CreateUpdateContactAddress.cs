// <copyright file="CreateUpdateContactAddress.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// Synchronizes contact information to custom address records and manages primary flags.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "contact"
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Pre Operation - Execution Mode: Synchronous - Deploy: Server.
    ///     Pre-Image: Not required
    ///
    /// Message: "Update" - Primary Entity: "contact"
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Pre Operation - Execution Mode: Synchronous - Deploy: Server.
    ///     Pre-Image: Required with fields: telephone1, mobilephone, emailaddress1, address1_line1, address1_line2,
    ///     address1_city, stt_stateid, stt_countyid, stt_zipcodeid.
    /// </remarks>
    public class CreateUpdateContactAddress : IPlugin
    {
        public const bool overridePreImage = true;
        private const int EMAILADDRESSTYPE = 924510000;
        private const int MAILINGADDRESSTYPE = 924510001;
        private const int BUSINESSPHONETYPE = 924510002;
        private const int MOBILEPHONETYPE = 924510003;
        private bool isUpdateMessage = false;
        private string stateAbbreviation;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("Starting CreateUpdateContactAddress Plugin");
            tracingService.Trace($"Message: {context.MessageName}, Depth: {context.Depth}");

            if (context.Depth > 5)
            {
                tracingService.Trace("Plugin depth exceeded 5, stopping execution to avoid infinite loops");
                return;
            }

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity)
                {
                    if (entity.LogicalName == "contact")
                    {
                        tracingService.Trace($"Processing Contact ID: {entity.Id}");
                        if (context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase))
                        {
                            this.CreateAddressRecord(service, tracingService, entity);
                            tracingService.Trace("CREATE operation completed successfully");
                        }
                        else if (context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase))
                        {
                            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
                            Entity postImage = context.PostEntityImages.Contains("PostImage") ? context.PostEntityImages["PostImage"] : null;
                            this.UpdateOrCreateAddressRecord(service, tracingService, preImage, postImage);
                            tracingService.Trace("UPDATE operation completed successfully");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in CreateUpdateContactAddressInfo plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in CreateUpdateContactAddressInfo plugin: {ex.ToString()}");
            }
        }

        private void UpdateOrCreateAddressRecord(IOrganizationService service, ITracingService tracingService, Entity PreImage, Entity PostImage)
        {
            if (PreImage.GetAttributeValue<string>("emailaddress1") != PostImage.GetAttributeValue<string>("emailaddress1"))
            {
                Entity existingPrimary = this.GetPrimaryAddressRecord(service, PostImage.Id, EMAILADDRESSTYPE);
                var updatedEmail = PostImage.GetAttributeValue<string>("emailaddress1");
                if (updatedEmail != null)
                {
                    Entity existingRecordWithoutPrimary = this.GetAddressRecordByValue(service, PostImage.Id, EMAILADDRESSTYPE, updatedEmail, "stt_emailaddress");
                    if (existingRecordWithoutPrimary != null)
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_isprimary"] = false;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary Email record");
                        }

                        Entity promoteRecord = new Entity("stt_address", existingRecordWithoutPrimary.Id);
                        promoteRecord["stt_isprimary"] = true;
                        service.Update(promoteRecord);
                        tracingService.Trace("Promoted existing Email record to primary");
                    }
                    else
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_emailaddress"] = PostImage.Contains("emailaddress1") && PostImage["emailaddress1"] != null ? PostImage.GetAttributeValue<string>("emailaddress1") : null;
                            demoteRecord["stt_address"] = PostImage.Contains("emailaddress1") && PostImage["emailaddress1"] != null ? PostImage.GetAttributeValue<string>("emailaddress1") : null;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary Email record");
                        }
                        else
                        {
                            Entity addressRecord = new Entity("stt_address");
                            addressRecord["stt_addresstypecode"] = new OptionSetValue(EMAILADDRESSTYPE);
                            addressRecord["stt_isprimary"] = true;
                            addressRecord["stt_contactid"] = new EntityReference("contact", PostImage.Id);
                            addressRecord["stt_address"] = PostImage.Contains("emailaddress1") && PostImage["emailaddress1"] != null ? PostImage.GetAttributeValue<string>("emailaddress1") : null;
                            addressRecord["stt_emailaddress"] = PostImage.Contains("emailaddress1") && PostImage["emailaddress1"] != null ? PostImage.GetAttributeValue<string>("emailaddress1") : null;
                            service.Create(addressRecord);
                            tracingService.Trace($"Created new primary record for emailaddress1");
                        }
                    }
                }
                else
                {
                    if (existingPrimary != null)
                    {
                        Entity addressRecord = new Entity("stt_address", existingPrimary.Id);
                        addressRecord["stt_address"] = string.Empty;
                        addressRecord["stt_emailaddress"] = string.Empty;
                        service.Update(addressRecord);
                        tracingService.Trace("updated primary emailaddress record.");
                    }
                }
            }

            if (PreImage.GetAttributeValue<string>("telephone1") != PostImage.GetAttributeValue<string>("telephone1"))
            {
                Entity existingPrimary = this.GetPrimaryAddressRecord(service, PostImage.Id, BUSINESSPHONETYPE);
                var updatedtelephone1 = PostImage.Contains("telephone1") ? PostImage.GetAttributeValue<string>("telephone1") : null;
                if (updatedtelephone1 != null)
                {
                    Entity existingRecordWithoutPrimary = this.GetAddressRecordByValue(service, PostImage.Id, BUSINESSPHONETYPE, updatedtelephone1, "stt_telephone1");
                    if (existingRecordWithoutPrimary != null)
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_isprimary"] = false;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary telephone1 record");
                        }

                        Entity promoteRecord = new Entity("stt_address", existingRecordWithoutPrimary.Id);
                        promoteRecord["stt_isprimary"] = true;
                        service.Update(promoteRecord);
                        tracingService.Trace("Promoted existing telephone1 record to primary");
                    }
                    else
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_telephone1"] = PostImage.Contains("telephone1") && PostImage["telephone1"] != null ? PostImage.GetAttributeValue<string>("telephone1") : null;
                            demoteRecord["stt_address"] = PostImage.Contains("telephone1") && PostImage["telephone1"] != null ? PostImage.GetAttributeValue<string>("telephone1") : null;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary telephone1 record");
                        }
                        else
                        {
                            Entity addressRecord = new Entity("stt_address");
                            addressRecord["stt_addresstypecode"] = new OptionSetValue(BUSINESSPHONETYPE);
                            addressRecord["stt_isprimary"] = true;
                            addressRecord["stt_contactid"] = new EntityReference("contact", PostImage.Id);
                            addressRecord["stt_telephone1"] = PostImage.Contains("telephone1") && PostImage["telephone1"] != null ? PostImage.GetAttributeValue<string>("telephone1") : null;
                            addressRecord["stt_address"] = PostImage.Contains("telephone1") && PostImage["telephone1"] != null ? PostImage.GetAttributeValue<string>("telephone1") : null;
                            service.Create(addressRecord);
                            tracingService.Trace($"Created new primary record for telephone1");
                        }
                    }
                }
                else
                {
                    if (existingPrimary != null)
                    {
                        Entity addressRecord = new Entity("stt_address", existingPrimary.Id);
                        addressRecord["stt_address"] = string.Empty;
                        addressRecord["stt_telephone1"] = string.Empty;
                        service.Update(addressRecord);
                        tracingService.Trace("updated primary telephone1 record.");
                    }
                }
            }

            if (PreImage.GetAttributeValue<string>("mobilephone") != PostImage.GetAttributeValue<string>("mobilephone"))
            {
                Entity existingPrimary = this.GetPrimaryAddressRecord(service, PostImage.Id, MOBILEPHONETYPE);
                var updatedtelephone1 = PostImage.GetAttributeValue<string>("mobilephone");
                if (updatedtelephone1 != null)
                {
                    Entity existingRecordWithoutPrimary = this.GetAddressRecordByValue(service, PostImage.Id, MOBILEPHONETYPE, updatedtelephone1, "stt_telephone1");
                    if (existingRecordWithoutPrimary != null)
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_isprimary"] = false;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary mobilephone record");
                        }

                        Entity promoteRecord = new Entity("stt_address", existingRecordWithoutPrimary.Id);
                        promoteRecord["stt_isprimary"] = true;
                        service.Update(promoteRecord);
                        tracingService.Trace("Promoted existing mobilephone record to primary");
                    }
                    else
                    {
                        if (existingPrimary != null)
                        {
                            Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                            demoteRecord["stt_telephone1"] = PostImage.Contains("mobilephone") && PostImage["mobilephone"] != null ? PostImage.GetAttributeValue<string>("mobilephone") : null;
                            demoteRecord["stt_address"] = PostImage.Contains("mobilephone") && PostImage["mobilephone"] != null ? PostImage.GetAttributeValue<string>("mobilephone") : null;
                            service.Update(demoteRecord);
                            tracingService.Trace("Demoted previous primary mobilephone record");
                        }
                        else
                        {
                            Entity addressRecord = new Entity("stt_address");
                            addressRecord["stt_addresstypecode"] = new OptionSetValue(MOBILEPHONETYPE);
                            addressRecord["stt_isprimary"] = true;
                            addressRecord["stt_contactid"] = new EntityReference("contact", PostImage.Id);
                            addressRecord["stt_telephone1"] = PostImage.Contains("mobilephone") && PostImage["mobilephone"] != null ? PostImage.GetAttributeValue<string>("mobilephone") : null;
                            addressRecord["stt_address"] = PostImage.Contains("mobilephone") && PostImage["mobilephone"] != null ? PostImage.GetAttributeValue<string>("mobilephone") : null;
                            service.Create(addressRecord);
                            tracingService.Trace($"Created new primary record for mobilephone");
                        }
                    }
                }
                else
                {
                    if (existingPrimary != null)
                    {
                        Entity addressRecord = new Entity("stt_address", existingPrimary.Id);
                        addressRecord["stt_telephone1"] = string.Empty;
                        addressRecord["stt_address"] = string.Empty;
                        service.Update(addressRecord);
                        tracingService.Trace("updated primary mobilephone record.");
                    }
                }
            }

            if (PreImage.GetAttributeValue<string>("address1_line1") != PostImage.GetAttributeValue<string>("address1_line1") ||
                PreImage.GetAttributeValue<string>("address1_line2") != PostImage.GetAttributeValue<string>("address1_line2") ||
                PreImage.GetAttributeValue<string>("address1_city") != PostImage.GetAttributeValue<string>("address1_city") ||
                PreImage.GetAttributeValue<EntityReference>("stt_stateid") != PostImage.GetAttributeValue<EntityReference>("stt_stateid") ||
                PreImage.GetAttributeValue<EntityReference>("stt_countyid") != PostImage.GetAttributeValue<EntityReference>("stt_countyid") ||
                PreImage.GetAttributeValue<EntityReference>("stt_zipcodeid") != PostImage.GetAttributeValue<EntityReference>("stt_zipcodeid"))
            {
                string address1 = PostImage.Contains("address1_line1") && PostImage.GetAttributeValue<string>("address1_line1") != null ? PostImage.GetAttributeValue<string>("address1_line1") : null;
                string address2 = PostImage.Contains("address1_line2") && PostImage.GetAttributeValue<string>("address1_line2") != null ? PostImage.GetAttributeValue<string>("address1_line2") : null;
                string city = PostImage.Contains("address1_city") && PostImage.GetAttributeValue<string>("address1_city") != null ? PostImage.GetAttributeValue<string>("address1_city") : null;
                EntityReference stateid = PostImage.Contains("stt_stateid") && PostImage.GetAttributeValue<EntityReference>("stt_stateid") != null ? PostImage.GetAttributeValue<EntityReference>("stt_stateid") : null;

                // set state Name with stt_abbreviation
                string stateName = string.Empty;
                if (stateid != null)
                {
                    var stateEntity = service.Retrieve("stt_state", stateid.Id, new ColumnSet("stt_abbreviation"));
                    stateName = stateEntity.Contains("stt_abbreviation") && stateEntity["stt_abbreviation"] != null ? stateEntity.GetAttributeValue<string>("stt_abbreviation") : string.Empty;
                    tracingService.Trace($"Successfully retrieved state abbreviation: '{stateName}'");
                }

                EntityReference countyid = PostImage.Contains("stt_countyid") && PostImage.GetAttributeValue<EntityReference>("stt_countyid") != null ? PostImage.GetAttributeValue<EntityReference>("stt_countyid") : null;
                EntityReference zipcodeid = PostImage.Contains("stt_zipcodeid") && PostImage.GetAttributeValue<EntityReference>("stt_zipcodeid") != null ? PostImage.GetAttributeValue<EntityReference>("stt_zipcodeid") : null;
                //string stateName = PostImage.GetAttributeValue<EntityReference>("stt_stateid")?.Name ?? string.Empty;
                string countyName = countyid != null ? PostImage.GetAttributeValue<EntityReference>("stt_countyid")?.Name : string.Empty;
                string zipName = zipcodeid != null ? PostImage.GetAttributeValue<EntityReference>("stt_zipcodeid")?.Name : string.Empty;

                // Build the address string (removes empty segments automatically)
                string fullAddress = string.Join(" - ", new[] { address1, address2, city, stateName, countyName, zipName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                Entity existingRecordWithSameValues = this.GetAddressRecordByAddressValues(service, PostImage.Id, MAILINGADDRESSTYPE, PostImage);
                if (existingRecordWithSameValues != null)
                {
                    // If found and not already primary, promote this record to primary
                    tracingService.Trace("Found existing Mailing Address record with same values, promoting to primary");
                    Entity existingPrimary = this.GetPrimaryAddressRecord(service, PostImage.Id, MAILINGADDRESSTYPE);
                    if (existingPrimary != null)
                    {
                        Entity demoteRecord = new Entity("stt_address", existingPrimary.Id);
                        demoteRecord["stt_isprimary"] = false;
                        service.Update(demoteRecord);
                        tracingService.Trace("Demoted previous primary mobilephone record");
                    }

                    // Now promote the matching record to primary
                    Entity promoteRecord = new Entity("stt_address", existingRecordWithSameValues.Id);
                    promoteRecord["stt_isprimary"] = true;
                    service.Update(promoteRecord);
                }
                else
                {
                    Entity existingPrimary = this.GetPrimaryAddressRecord(service, PostImage.Id, MAILINGADDRESSTYPE);
                    if (existingPrimary != null)
                    {
                        // Update the existing primary record with any changes
                        Entity updateRecord = new Entity("stt_address", existingPrimary.Id);
                        updateRecord["stt_address1"] = address1;
                        updateRecord["stt_address2"] = address2;
                        updateRecord["stt_city"] = city;
                        updateRecord["stt_stateid"] = stateid;
                        updateRecord["stt_countyid"] = countyid;
                        updateRecord["stt_zipcodeid"] = zipcodeid;
                        updateRecord["stt_address"] = fullAddress;
                        service.Update(updateRecord);
                        tracingService.Trace("Existing Mailing Address record is already primary,address updated.");
                    }
                    else
                    {
                        Entity createAddressRecord = new Entity("stt_address");
                        createAddressRecord["stt_addresstypecode"] = new OptionSetValue(MAILINGADDRESSTYPE);
                        createAddressRecord["stt_address1"] = address1;
                        createAddressRecord["stt_address2"] = address2;
                        createAddressRecord["stt_city"] = city;
                        createAddressRecord["stt_stateid"] = stateid;
                        createAddressRecord["stt_countyid"] = countyid;
                        createAddressRecord["stt_zipcodeid"] = zipcodeid;
                        createAddressRecord["stt_address"] = fullAddress;
                        createAddressRecord["stt_isprimary"] = true;
                        createAddressRecord["stt_contactid"] = new EntityReference("contact", PostImage.Id);
                        service.Create(createAddressRecord);
                        tracingService.Trace("Created new Mailing Address record as a primary.");
                    }
                }
            }
        }

        private Entity GetAddressRecordByAddressValues(IOrganizationService service, Guid contactId, int addressTypeCode, Entity postImage)
        {
            QueryExpression query = new QueryExpression("stt_address");
            query.ColumnSet = new ColumnSet("stt_addressid", "stt_address1", "stt_address2", "stt_city", "stt_stateid", "stt_countyid", "stt_zipcodeid", "stt_isprimary", "stt_address");
            query.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            query.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, addressTypeCode);
            query.Criteria.AddCondition("stt_isprimary", ConditionOperator.Equal, false);

            // Use FilterExpression for better null handling
            FilterExpression filter = new FilterExpression(LogicalOperator.And);

            // Handle string fields (address1, address2, city)
            string[] stringFields = { "address1_line1", "address1_line2", "address1_city" };
            string[] targetFields = { "stt_address1", "stt_address2", "stt_city" };

            for (int i = 0; i < stringFields.Length; i++)
            {
                if (postImage.Contains(stringFields[i]))
                {
                    object value = postImage[stringFields[i]];
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        filter.AddCondition(targetFields[i], ConditionOperator.Equal, value.ToString());
                    }
                    else
                    {
                        // Handle both null and empty string cases
                        filter.AddCondition(new ConditionExpression(targetFields[i], ConditionOperator.Null));
                        filter.AddCondition(new ConditionExpression(targetFields[i], ConditionOperator.Equal, ""));
                    }
                }
            }

            // Handle lookup fields (state, county, zipcode)
            EntityReference[] lookups =
            {
                postImage.Contains("stt_stateid") ? postImage.GetAttributeValue<EntityReference>("stt_stateid") : null,
                postImage.Contains("stt_countyid") ? postImage.GetAttributeValue<EntityReference>("stt_countyid") : null,
                postImage.Contains("stt_zipcodeid") ? postImage.GetAttributeValue<EntityReference>("stt_zipcodeid") : null,
            };

            string[] lookupFields = { "stt_stateid", "stt_countyid", "stt_zipcodeid" };

            for (int i = 0; i < lookups.Length; i++)
            {
                if (lookups[i] != null)
                {
                    filter.AddCondition(lookupFields[i], ConditionOperator.Equal, lookups[i].Id);
                }
                else
                {
                    filter.AddCondition(lookupFields[i], ConditionOperator.Null);
                }
            }

            query.Criteria.AddFilter(filter);

            EntityCollection results = service.RetrieveMultiple(query);
            return results.Entities.FirstOrDefault();
        }

        private Entity GetPrimaryAddressRecord(IOrganizationService service, Guid contactId, int addressTypeCode)
        {
            QueryExpression query = new QueryExpression("stt_address");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            query.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, addressTypeCode);
            query.Criteria.AddCondition("stt_isprimary", ConditionOperator.Equal, true);

            EntityCollection results = service.RetrieveMultiple(query);
            return results.Entities.FirstOrDefault();
        }

        private Entity GetAddressRecordByValue(IOrganizationService service, Guid contactId, int addressTypeCode, string value, string logicalName)
        {
            QueryExpression query = new QueryExpression("stt_address");
            query.ColumnSet = new ColumnSet("stt_addressid", "stt_emailaddress", "stt_telephone1", "stt_isprimary", "stt_address");
            query.Criteria.AddCondition("stt_contactid", ConditionOperator.Equal, contactId);
            query.Criteria.AddCondition("stt_addresstypecode", ConditionOperator.Equal, addressTypeCode);
            query.Criteria.AddCondition(logicalName, ConditionOperator.Equal, value);
            query.Criteria.AddCondition("stt_isprimary", ConditionOperator.Equal, false);

            EntityCollection results = service.RetrieveMultiple(query);
            return results.Entities.FirstOrDefault();
        }

        private void CreateAddressRecord(IOrganizationService service, ITracingService tracingService, Entity contact)
        {
            if (contact.Contains("telephone1") && contact["telephone1"] != null)
            {
                Entity addressRecord = new Entity("stt_address");
                addressRecord["stt_addresstypecode"] = new OptionSetValue(BUSINESSPHONETYPE);
                addressRecord["stt_isprimary"] = true;
                addressRecord["stt_contactid"] = new EntityReference("contact", contact.Id);
                addressRecord["stt_telephone1"] = contact.GetAttributeValue<string>("telephone1");
                addressRecord["stt_address"] = contact.GetAttributeValue<string>("telephone1");
                service.Create(addressRecord);
                tracingService.Trace($"Created new primary record for stt_telephone1");
            }

            if (contact.Contains("mobilephone") && contact["mobilephone"] != null)
            {
                Entity addressRecord = new Entity("stt_address");
                addressRecord["stt_addresstypecode"] = new OptionSetValue(MOBILEPHONETYPE);
                addressRecord["stt_isprimary"] = true;
                addressRecord["stt_contactid"] = new EntityReference("contact", contact.Id);
                addressRecord["stt_telephone1"] = contact.GetAttributeValue<string>("mobilephone");
                addressRecord["stt_address"] = contact.GetAttributeValue<string>("mobilephone");
                service.Create(addressRecord);
                tracingService.Trace($"Created new primary record for mobilephone");
            }

            if (contact.Contains("emailaddress1") && contact["emailaddress1"] != null)
            {
                Entity addressRecord = new Entity("stt_address");
                addressRecord["stt_addresstypecode"] = new OptionSetValue(EMAILADDRESSTYPE);
                addressRecord["stt_isprimary"] = true;
                addressRecord["stt_contactid"] = new EntityReference("contact", contact.Id);
                addressRecord["stt_emailaddress"] = contact.GetAttributeValue<string>("emailaddress1");
                addressRecord["stt_address"] = contact.GetAttributeValue<string>("emailaddress1");
                service.Create(addressRecord);
                tracingService.Trace($"Created new primary record for emailaddress1");
            }

            if (contact.Contains("address1_line1") || contact.Contains("address1_line2") || contact.Contains("address1_city") || contact.Contains("stt_stateid") || contact.Contains("stt_countyid") || contact.Contains("stt_zipcodeid"))
            {
                tracingService.Trace($"Address Record Creating..");
                string address1 = string.Empty;
                string address2 = string.Empty;
                string city = string.Empty;
                string stateName = string.Empty;
                string countyName = string.Empty;
                string zipName  = string.Empty;
                Entity address = new Entity("stt_address");
                var isAddressCreated = false;

                // Map string fields
                if (contact.Contains("address1_line1") && contact["address1_line1"] != null)
                {
                    address["stt_address1"] = contact.GetAttributeValue<string>("address1_line1");
                    address1 = contact.GetAttributeValue<string>("address1_line1");
                    isAddressCreated = true;
                }

                if (contact.Contains("address1_line2") && contact["address1_line2"] != null)
                {
                    address["stt_address2"] = contact.GetAttributeValue<string>("address1_line2");
                    address2 = contact.GetAttributeValue<string>("address1_line2");
                    isAddressCreated = true;
                }

                if (contact.Contains("address1_city") && contact["address1_city"] != null)
                {
                    address["stt_city"] = contact.GetAttributeValue<string>("address1_city");
                    city = contact.GetAttributeValue<string>("address1_city");
                    isAddressCreated = true;
                }

                // Map lookup fields
                if (contact.Contains("stt_stateid") && contact["stt_stateid"] != null)
                {
                    address["stt_stateid"] = contact.GetAttributeValue<EntityReference>("stt_stateid");
                   // stateName = contact.GetAttributeValue<EntityReference>("stt_stateid").Name;
                    Entity entityState = service.Retrieve("stt_state", contact.GetAttributeValue<EntityReference>("stt_stateid").Id, new ColumnSet("stt_name"));
                    stateName = entityState.Contains("stt_name") && entityState["stt_name"] != null ?
                        entityState.GetAttributeValue<string>("stt_name") : string.Empty;
                    isAddressCreated = true;
                }

                if (contact.Contains("stt_countyid") && contact["stt_countyid"] != null)
                {
                    address["stt_countyid"] = contact.GetAttributeValue<EntityReference>("stt_countyid");
                    Entity entityCounty = service.Retrieve("stt_county", contact.GetAttributeValue<EntityReference>("stt_countyid").Id, new ColumnSet("stt_name"));
                    countyName = entityCounty.Contains("stt_name") && entityCounty["stt_name"] != null?
                        entityCounty.GetAttributeValue<string>("stt_name"):string.Empty;
                    isAddressCreated = true;
                }

                if (contact.Contains("stt_zipcodeid") && contact["stt_zipcodeid"] != null)
                {
                    address["stt_zipcodeid"] = contact.GetAttributeValue<EntityReference>("stt_zipcodeid");

                    Entity entityZipCode = service.Retrieve("stt_zipcode", contact.GetAttributeValue<EntityReference>("stt_zipcodeid").Id, new ColumnSet("stt_name"));
                    zipName = entityZipCode.Contains("stt_name") && entityZipCode["stt_name"] != null ?
                        entityZipCode.GetAttributeValue<string>("stt_name") : string.Empty;

                    isAddressCreated = true;
                }
                string fullAddress = string.Join(" - ", new[] { address1, address2, city, stateName, countyName, zipName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                if (isAddressCreated == true)
                {
                    address["stt_isprimary"] = true;
                    address["stt_contactid"] = new EntityReference("contact", contact.Id);
                    address["stt_addresstypecode"] = new OptionSetValue(MAILINGADDRESSTYPE);
                    address["stt_address"] = fullAddress;
                    service.Create(address);
                    tracingService.Trace("Address record created successfully.");
                }
            }
        }
    }
}
