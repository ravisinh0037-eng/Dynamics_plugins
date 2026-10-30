// <copyright file="CreatePostOpContactCreateClientMSADetail.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using VEER.Plugins.Utils;

    /// <summary>
    /// Creates a new Client Details record for a Contact and each Market the user belongs to via Market Teams,
    /// when a Contact is created and the CreatedBy user is not the PowerAppServiceAccount.
    /// For each stt_marketteam the user belongs to, the plugin finds the associated stt_market and creates a stt_clientdetail
    /// if one does not already exist for the Contact and Market. Also creates a stt_clientmsadetail for the Contact and MSA if not present.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "Contact" - Secondary Entity: n/a
    ///     Run as: Service Account - Execution Order: 2
    ///     Stage: Post Operation - Execution Mode: Asynchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreatePostOpContactCreateClientMSADetail : IPlugin
    {
        private string bdoPosition = string.Empty;
        private string isaPosition = string.Empty;
        private string positionField = string.Empty;
        private int originalSource = 0;
        private string serviceAccount = string.Empty;

        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the plugin execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId); // Executes as the user who triggered the plugin
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("ContactMarketTeamProcessor Plugin Started.");

            // Validate that the plugin executes on the correct stage and message
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
            {
                if (targetEntity.LogicalName != "contact" || context.MessageName != "Create")
                {
                    tracingService.Trace($"Plugin executed on wrong entity/message/stage. LogicalName: {targetEntity.LogicalName}, Message: {context.MessageName}, Stage: {context.Stage}");
                    return;
                }

                try
                {
                    // The newly created Contact is available in PostOperation
                    // Retrieve the CreatedBy (it's an EntityReference to systemuser)
                    EntityReference createdByRef = null;
                    if (targetEntity.Contains("createdby"))
                    {
                        createdByRef = targetEntity.GetAttributeValue<EntityReference>("createdby");
                        tracingService.Trace($"Contact CreatedBy User ID: {createdByRef.Id}");
                    }
                    else
                    {
                        tracingService.Trace("Contact does not contain 'createdby' attribute in target entity.");
                        return;
                    }

                    // Retrieve Enviroments Variables
                    this.bdoPosition = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_BDO_Position_Guid");
                    this.isaPosition = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_ISA_Position_Guid");
                    this.serviceAccount = EnvironmentVariableHelper.GetEnvironmentVariableValue(service, "stt_Service_Account_Guid");
                    tracingService.Trace($"BDO GUID: {this.bdoPosition}");
                    tracingService.Trace($"ISA GUID: {this.isaPosition}");
                    tracingService.Trace($"Service Account GUID: {this.serviceAccount}");

                    // 1. Check if CreatedBy is equal to PowerAppServiceAccount
                    if (createdByRef.Id.ToString() == this.serviceAccount)
                    {
                        tracingService.Trace("Contact created by PowerAppServiceAccount. No further action.");
                        return;
                    }

                    // Checking for the User Position
                    Entity user = service.Retrieve("systemuser", createdByRef.Id, new ColumnSet("positionid"));

                    if (user == null || !user.Contains("positionid"))
                    {
                        tracingService.Trace($"User {createdByRef.Name} does not have a position assigned. No stt_clientdetail created.");
                        return;
                    }

                    EntityReference positionRef = user.GetAttributeValue<EntityReference>("positionid");
                    tracingService.Trace($"User {createdByRef.Name} has position: {positionRef.Name} ({positionRef.Id})");

                    if (positionRef.Id.ToString() == this.bdoPosition)
                    {
                        this.positionField = "stt_bdoid";
                        this.originalSource = 924510001;
                    }
                    else
                    {
                        if (positionRef.Id.ToString() == this.isaPosition)
                        {
                            this.positionField = "stt_isaid";
                            this.originalSource = 924510000;
                        }
                        else
                        {
                            tracingService.Trace($"User {createdByRef.Name} does not have a recognized position. No stt_clientdetail created.");
                            return;
                        }
                    }

                    // 2. Search for all stt_marketteam records that the user (CreatedBy) belongs to
                    QueryExpression teamQuery = new QueryExpression("stt_marketteam")
                    {
                        ColumnSet = new ColumnSet("stt_marketteamid", "stt_marketid", "stt_bdoid", "stt_isaid"),
                        Criteria = new FilterExpression(LogicalOperator.And)
                        {
                            Conditions =
                            {
                                new ConditionExpression(this.positionField, ConditionOperator.Equal, createdByRef.Id),
                            },
                        },
                    };

                    EntityCollection marketTeams = service.RetrieveMultiple(teamQuery);
                    tracingService.Trace($"Found {marketTeams.Entities.Count} market teams for user {createdByRef.Name}.");

                    if (marketTeams.Entities.Count == 0)
                    {
                        tracingService.Trace("No market teams found for the user. No stt_clientdetail created.");
                        return;
                    }

                    // 3. Iterate over each stt_marketteam found
                    foreach (Entity marketTeam in marketTeams.Entities)
                    {
                        EntityReference isaTeamRef = marketTeam.GetAttributeValue<EntityReference>("stt_isaid");
                        EntityReference bdoTeamRef = marketTeam.GetAttributeValue<EntityReference>("stt_bdoid");

                        // Get the EntityReference to the associated stt_market for this marketing team
                        EntityReference marketRef = null;
                        if (marketTeam.Contains("stt_marketid"))
                        {
                            marketRef = marketTeam.GetAttributeValue<EntityReference>("stt_marketid");
                            tracingService.Trace($"Processing market team '{marketTeam.Id}' for market: {marketRef.Name} ({marketRef.Id})");
                        }
                        else
                        {
                            tracingService.Trace($"Market team {marketTeam.Id} does not have an associated market. Skipping.");
                            continue;
                        }

                        // Get the owner and msa for the market
                        Entity market = service.Retrieve(marketRef.LogicalName, marketRef.Id, new ColumnSet("stt_outreachownercode", "ownerid", "stt_msaid", "stt_name"));
                        EntityReference ownerReference = market.GetAttributeValue<EntityReference>("ownerid");
                        EntityReference msaReference = market.GetAttributeValue<EntityReference>("stt_msaid");

                        // 4. Check if already exists a stt_clientdetail for this contact and market
                        var clientDetailquery = new QueryExpression("stt_clientdetails")
                        {
                            TopCount = 50,
                            ColumnSet = new ColumnSet("stt_clientdetailsid"),
                            Criteria =
                            {
                                Conditions =
                                {
                                    new ConditionExpression("stt_contactid", ConditionOperator.Equal, targetEntity.Id),
                                    new ConditionExpression("stt_marketid", ConditionOperator.Equal, market.Id),
                                },
                            },
                        };
                        EntityCollection existingDetails = service.RetrieveMultiple(clientDetailquery);
                        if (existingDetails.Entities.Count > 0)
                        {
                            tracingService.Trace($"stt_clientdetail already exists for Contact '{targetEntity.Id}' and Market '{marketRef.Id}'. Skipping creation.");
                            continue; // Skip creating a new record if it already exists
                        }
                        (Entity lifecycleSettingTrans, Entity lifecycleSettingAct) = this.SearchLifecycleSettings(service, tracingService);
                        // 5. Create a new stt_clientdetail record for each stt_market found
                        Entity clientDetail = new Entity("stt_clientdetails");
                        clientDetail["stt_name"] = $"{targetEntity.Attributes["fullname"]} - {market["stt_name"]}";

                        // Set relationships to Contact and Market
                        clientDetail["stt_contactid"] = new EntityReference("contact", targetEntity.Id);
                        clientDetail["stt_marketid"] = new EntityReference("stt_market", market.Id);
                        clientDetail["stt_clientstagecode"] = new OptionSetValue(924510000);
                        clientDetail["stt_originalsource"] = new OptionSetValue(this.originalSource);
                        clientDetail["ownerid"] = new EntityReference(ownerReference.LogicalName, ownerReference.Id);
                        // Check position of user BDO/ISA
                        if (positionRef.Id.ToString() == this.isaPosition)
                        {
                            clientDetail["stt_isaid"] = new EntityReference(isaTeamRef.LogicalName, isaTeamRef.Id);
                        }

                        if (positionRef.Id.ToString() == this.bdoPosition)
                        {
                            clientDetail["stt_bdoid"] = new EntityReference(bdoTeamRef.LogicalName, bdoTeamRef.Id);
                        }
                        clientDetail["stt_lifecyclesettingstransactionsid"] = new EntityReference(lifecycleSettingTrans.LogicalName, lifecycleSettingTrans.Id);
                        clientDetail["stt_lifecyclesettingsactivitiesid"] = new EntityReference(lifecycleSettingAct.LogicalName, lifecycleSettingAct.Id);
                        service.Create(clientDetail);
                        tracingService.Trace($"Successfully created stt_clientdetail for Contact '{targetEntity.Id}' and Market '{marketRef.Id}'.");

                        // 6. Check if already exists a stt_clientmsadetail for this contact and MSA
                        var clientMSAquery = new QueryExpression("stt_clientmsadetails")
                        {
                            TopCount = 50,
                            ColumnSet = new ColumnSet("stt_clientmsadetailsid"),
                            Criteria =
                            {
                                Conditions =
                                {
                                    new ConditionExpression("stt_contactid", ConditionOperator.Equal, targetEntity.Id),
                                    new ConditionExpression("stt_msaid", ConditionOperator.Equal, msaReference.Id),
                                },
                            },
                        };
                        EntityCollection existingMSADetails = service.RetrieveMultiple(clientMSAquery);
                        if (existingMSADetails.Entities.Count > 0)
                        {
                            tracingService.Trace($"stt_clientmsadetail already exists for Contact '{targetEntity.Id}' and MSA '{msaReference?.Id}'. Skipping creation.");
                            continue;
                        }

                        // 7. Creates MSA Details for each market on that MSA
                        if (msaReference != null)
                        {
                            Entity msaDetail = new Entity("stt_clientmsadetails");
                            msaDetail["stt_name"] = $"{targetEntity.Attributes["fullname"]} - {msaReference.Name}";
                            msaDetail["stt_contactid"] = new EntityReference("contact", targetEntity.Id);
                            msaDetail["stt_msaid"] = new EntityReference(msaReference.LogicalName, msaReference.Id);
                            msaDetail["ownerid"] = new EntityReference(ownerReference.LogicalName, ownerReference.Id);
                            // msaDetail["stt_productiongradecode"] = new OptionSetValue(198730003);
                            // msaDetail["stt_productionscore"] = new decimal(0);
                            service.Create(msaDetail);
                            tracingService.Trace($"Successfully created stt_clientmsadetail for Contact '{targetEntity.Id}' and Market '{msaReference.Id}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace($"An error occurred in CreatePostOpContactCreateClientMSADetail: {ex.Message}");
                }
            }
            else
            {
                tracingService.Trace("Target entity not found in InputParameters.");
            }

            tracingService.Trace("CreatePostOpContactCreateClientMSADetail Plugin Finished.");
        }

        private (Entity lifecycleSettingTrans, Entity lifecycleSettingAct) SearchLifecycleSettings(IOrganizationService service, ITracingService tracingService)
        {
            // Set Condition Values
            var query_stt_typecode = 924510000;
            var query_statecode = 0;

            // Instantiate QueryExpression query
            var query = new QueryExpression("stt_stewartsettings");
            query.TopCount = 50;

            // Add columns to query.ColumnSet
            query.ColumnSet.AddColumns("stt_stewartsettingsid", "stt_name", "stt_purposecode");

            // Add conditions to query.Criteria
            query.Criteria.AddCondition("stt_typecode", ConditionOperator.Equal, query_stt_typecode);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);

            EntityCollection lifecycleSettingsResults = service.RetrieveMultiple(query);

            // Initialize variables to store the results
            Entity lifecycleSettingTrans = null;
            Entity lifecycleSettingAct = null;

            // Iterate through the results and assign to the appropriate variable
            foreach (Entity lifecycleSetting in lifecycleSettingsResults.Entities)
            {
                int purposeCode = lifecycleSetting.GetAttributeValue<OptionSetValue>("stt_purposecode")?.Value ?? -1;

                if (purposeCode == 924510001)
                {
                    lifecycleSettingTrans = lifecycleSetting;
                    tracingService.Trace($"Assigned lifecycle setting to Trans: {lifecycleSetting.GetAttributeValue<string>("stt_name")}");
                }
                else
                {
                    lifecycleSettingAct = lifecycleSetting;
                    tracingService.Trace($"Assigned lifecycle setting to Act: {lifecycleSetting.GetAttributeValue<string>("stt_name")}");
                }
            }

            return (lifecycleSettingTrans, lifecycleSettingAct);
        }
    }
}
