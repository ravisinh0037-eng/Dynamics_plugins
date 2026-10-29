// <copyright file="CreatePostOpClientDetailsSequence.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Remoting.Services;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Metadata;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// On creation of a "Client Details" record where the "Stage" field is equal to "Cold Producer",
    /// retrieves the Sequence Assignment rule for the Market assigned to the Client Details and attaches the Sequence to the Contact.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "Client Detail" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreatePostOpClientDetailsSequence : IPlugin
    {
        int clientStage = 924510000;
        int isaOwned = 924510000;
        int bdoOwned = 924510001;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
                {
                    if (targetEntity.LogicalName == "stt_clientdetails")
                    {
                        // Filter: stt_clientstagecode equals ColdProducer
                        if (targetEntity.Contains("stt_clientstagecode") && targetEntity["stt_clientstagecode"] != null)
                        {
                            OptionSetValue clientStageCode = targetEntity.GetAttributeValue<OptionSetValue>("stt_clientstagecode");
                            if (clientStageCode != null && clientStageCode.Value == this.clientStage)
                            {
                                tracingService.Trace("Client Details stage code is Cold Producer");
                                Entity clientDetails = service.Retrieve(targetEntity.LogicalName, targetEntity.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("stt_marketid", "stt_contactid", "stt_bdoid", "stt_isaid", "stt_originalsource"));
                                tracingService.Trace($"Client Details GUID: {clientDetails.Id}");
                                EntityReference bdoUserReference = clientDetails.GetAttributeValue<EntityReference>("stt_bdoid");
                                tracingService.Trace($"BDO User Reference: {bdoUserReference?.Name}");
                                EntityReference isaReference = clientDetails.GetAttributeValue<EntityReference>("stt_isaid");
                                tracingService.Trace($"ISA Reference: {isaReference?.Name}");
                                if (clientDetails.Contains("stt_marketid") && clientDetails.Contains("stt_contactid"))
                                {
                                    tracingService.Trace("Client Details have Market and Contact, searching the Market...");

                                    // Set the contact
                                    EntityReference contactRef = clientDetails.GetAttributeValue<EntityReference>("stt_contactid");
                                    Entity market = service.Retrieve("stt_market", clientDetails.GetAttributeValue<EntityReference>("stt_marketid").Id, new ColumnSet("stt_outreachownercode", "stt_commercialmarket"));
                                    tracingService.Trace($"Market GUID: {market.Id}");

                                    // Commercial Client Details are governed by the DOCommercial plugin; NCS sequence assignment does not apply.
                                    if (market.GetAttributeValue<bool>("stt_commercialmarket"))
                                    {
                                        tracingService.Trace("Market is a Commercial Market. Skipping sequence assignation.");
                                        return;
                                    }

                                    OptionSetValue originalSource = clientDetails.GetAttributeValue<OptionSetValue>("stt_originalsource");
                                    if (originalSource == null)
                                    {
                                        tracingService.Trace("Client Details doesn't have Original Source. Skipping sequence assignation.");
                                        return;
                                    }

                                    tracingService.Trace($"stt_originalsource value: {originalSource.Value}");

                                    string originalSourceName = GetOptionSetLabel("stt_clientdetails", "stt_originalsource", originalSource.Value, service);

                                    tracingService.Trace($"stt_outreachownercode name: {originalSourceName}");

                                    if (originalSourceName != null && (originalSourceName == "NDC" || originalSourceName == "Directing Client"))
                                    {
                                        // Set the ref for Market
                                        if (market.Contains("stt_outreachownercode") && market["stt_outreachownercode"] != null)
                                        {
                                            OptionSetValue outreachOwnerCode = (OptionSetValue)market["stt_outreachownercode"];
                                            int ownerCodeValue = outreachOwnerCode.Value;

                                            tracingService.Trace($"stt_outreachownercode value: {ownerCodeValue}");

                                            if (ownerCodeValue == this.isaOwned) // "ISA-owned"
                                            {
                                                tracingService.Trace("stt_outreachownercode is ISA-owned. Performing ISA-related actions.");
                                                if (isaReference == null)
                                                {
                                                    tracingService.Trace("Client Details doesn't have ISA, skipping sequence assignation");
                                                    return;
                                                }

                                                CreateSequenceAndAssignIt(service, contactRef, isaReference, tracingService, originalSourceName);
                                            }
                                            else if (ownerCodeValue == this.bdoOwned) // "BDO-owned"
                                            {
                                                tracingService.Trace("stt_outreachownercode is BDO-owned. Performing BDO-related actions.");
                                                if (bdoUserReference == null)
                                                {
                                                    tracingService.Trace("Client Details doesn't have BDO, skipping sequence assignation");
                                                    return;
                                                }

                                                CreateSequenceAndAssignIt(service, contactRef, bdoUserReference, tracingService, originalSourceName);
                                            }
                                            else
                                            {
                                                tracingService.Trace($"stt_outreachownercode has an unexpected value: {ownerCodeValue}. Skipping specific actions.");
                                            }
                                        }
                                        else
                                        {
                                            tracingService.Trace("stt_outreachownercode is null in Market, skipping sequence assignation.");
                                        }
                                    }
                                    else if (originalSourceName != null)
                                    {
                                        if (originalSourceName == "BDO")
                                        {
                                            CreateSequenceAndAssignIt(service, contactRef, bdoUserReference, tracingService, originalSourceName);
                                        }
                                        else if (originalSourceName == "ISA")
                                        {
                                            CreateSequenceAndAssignIt(service, contactRef, isaReference, tracingService, originalSourceName);
                                        }
                                        else
                                        {
                                            tracingService.Trace("Client Details doesn't have BDO or ISA, skipping sequence assignation");
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    tracingService.Trace("Client Details does not contain Market or Contact");
                                }
                            }
                            else
                            {
                                tracingService.Trace("Client Details is not Cold Producer");
                            }
                        }
                        else
                        {
                            tracingService.Trace("Client Details doesn't have Client Stage");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in CreatePostOpClientDetailsSequence plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in CreatePostOpClientDetailsSequence plugin: {ex.ToString()}");
            }
        }

        private static void CreateSequenceAndAssignIt(IOrganizationService service, EntityReference contactRef, EntityReference userRef, ITracingService tracingService, string originalSource)
        {
            // Search if already exist Sequences Targets for that Contact
            QueryExpression sequenceTargeQuery = new QueryExpression("msdyn_sequencetarget");
            sequenceTargeQuery.Criteria.AddCondition("msdyn_target", ConditionOperator.Equal, contactRef.Id);
            sequenceTargeQuery.Criteria.AddCondition("ownerid", ConditionOperator.Equal, userRef.Id);
            sequenceTargeQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0); // Active

            // Add link-entity seq
            var seq = sequenceTargeQuery.AddLink("msdyn_sequence", "msdyn_parentsequence", "msdyn_sequenceid");
            seq.EntityAlias = "seq";

            // Add conditions to seq.LinkCriteria
            seq.LinkCriteria.AddCondition("msdyn_name", ConditionOperator.BeginsWith, originalSource);

            // Add filter to sequence name that contains originalSource define if is by parent sequence or applied sequence instance field in msdyn_sequencetarget entity
            EntityCollection sequenceTargetResults = service.RetrieveMultiple(sequenceTargeQuery);

            if (sequenceTargetResults.Entities.Count > 0)
            {
                tracingService.Trace("The Contact has already assign a Sequence, skipping operation");
                return;
            }

            // Get the sequence
            string sequenceFetch = $@"<fetch top='50'>
                                      <entity name='msdyn_sequence'>
                                        <filter>
                                          <condition attribute='statecode' operator='eq' value='1' />
                                        </filter>
                                        <link-entity name='msdyn_salestag_msdyn_sequence' from='msdyn_sequenceid' to='msdyn_sequenceid' intersect='true'>
                                          <link-entity name='msdyn_salestag' from='msdyn_salestagid' to='msdyn_salestagid' alias='T'>
                                            <attribute name='msdyn_tagname' />
                                            <filter>
                                              <condition attribute='msdyn_tagname' operator='eq' value='{originalSource}' />
                                            </filter>
                                            <link-entity name='msdyn_salestag' from='msdyn_salestagid' to='msdyn_parenttag' alias='parentTag'>
                                              <filter>
                                                <condition attribute='msdyn_tagname' operator='eq' value='Original Source' />
                                              </filter>
                                            </link-entity>
                                          </link-entity>
                                        </link-entity>
                                      </entity>
                                    </fetch>";
            EntityCollection sequenceEntities = service.RetrieveMultiple(new FetchExpression(sequenceFetch));
            if (sequenceEntities.Entities.Count > 0)
            {
                Guid sequenceId = sequenceEntities.Entities[0].GetAttributeValue<Guid>("msdyn_sequenceid");
                tracingService.Trace($"Sequence found with ID: {sequenceId}");
                tracingService.Trace($"contact id: {contactRef.Id}");

                // Create the "Sequence Target" intermediate table
                Entity sequenceTarget = new Entity("msdyn_sequencetarget"); // entity logical name
                sequenceTarget["msdyn_appliedsequenceinstance"] = new EntityReference("msdyn_sequence", sequenceId);
                sequenceTarget["msdyn_target"] = new EntityReference("contact", contactRef.Id);
                Random random = new Random();
                sequenceTarget["msdyn_sequencetargetuniquekey"] = random.Next().ToString();
                sequenceTarget["msdyn_currentsteptype"] = new OptionSetValue(0);
                sequenceTarget["msdyn_parentsequence"] = new EntityReference("msdyn_sequence", sequenceId);
                sequenceTarget["msdyn_name"] = "TEST";
                sequenceTarget["msdyn_regarding"] = "{\"etn\":\"contact\",\"id\":\"" + contactRef.Id + "\"}";
                sequenceTarget["ownerid"] = new EntityReference("systemuser", userRef.Id);

                service.Create(sequenceTarget);
            }
            else
            {
                tracingService.Trace($"Sequence with the msdyn_name: Test doesn't exist");
            }
        }

        private static string GetOptionSetLabel(string entityName, string attributeName, int optionSetValue, IOrganizationService service)
        {
            try
            {
                var request = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = attributeName,
                    RetrieveAsIfPublished = true,
                };

                var response = (RetrieveAttributeResponse)service.Execute(request);
                var metadata = (EnumAttributeMetadata)response.AttributeMetadata;

                var option = metadata.OptionSet.Options
                    .FirstOrDefault(o => o.Value == optionSetValue);

                return option?.Label?.UserLocalizedLabel?.Label ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
