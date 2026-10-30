// <copyright file="CreatePostOpPhonecallTwilio.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using VEER.Plugins.Utils;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// When a phonecall is created from Twilio, checking if there is a phonecall created before and updating it. Also to assign market.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "Phonecall" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Asynchronous-Deploy; Server.
    ///
    /// Images:
    ///     postImage: Attributes: activityid, from, to, description, actualstart, actualend, actualdurationminutes, stt_recordurl.
    /// </remarks>
    public class CreatePostOpPhonecallTwilio : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace($"Starting CreatePostOpPhonecallTwilio plugin... ");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity phonecallFromTrigger)
                {
                    tracingService.Trace($"Is it a phonecall activity from twilio?: {phonecallFromTrigger.GetAttributeValue<bool>("stt_fromtwilio")}");

                    if (phonecallFromTrigger.LogicalName == "phonecall" && context.MessageName.ToLower() == "create" && phonecallFromTrigger.GetAttributeValue<bool>("stt_fromtwilio") == true)
                    {
                        var phonecallId = phonecallFromTrigger.Id;
                        var fromId = string.Empty;
                        var toId = string.Empty;

                        // Taking activity parties: From and To.
                        if (phonecallFromTrigger.Attributes.Contains("from"))
                        {
                            var fromParties = phonecallFromTrigger["from"] as EntityCollection;
                            foreach (var party in fromParties.Entities)
                            {
                                fromId = ((EntityReference)party["partyid"]).Id.ToString();
                            }
                        }

                        if (phonecallFromTrigger.Attributes.Contains("to"))
                        {
                            var toParties = phonecallFromTrigger["to"] as EntityCollection;
                            foreach (var party in toParties.Entities)
                            {
                                toId = ((EntityReference)party["partyid"]).Id.ToString();
                            }
                        }

                        // Mapping correct status of the phone call based on the disposition code that comes from Twilio
                        OptionSetValue phoneCallStatusReason = new OptionSetValue();
                        OptionSetValue callDispositionCode = phonecallFromTrigger.ContainsData("stt_calldispositioncode") ? phonecallFromTrigger.GetAttributeValue<OptionSetValue>("stt_calldispositioncode") : new OptionSetValue();

                        switch (callDispositionCode.Value)
                        {
                            // Booked a Meeting
                            case 198730000:
                                phoneCallStatusReason.Value = 2;
                                break;

                            // Not interested
                            case 198730001:
                                phoneCallStatusReason.Value = 4;
                                break;

                            // Something else
                            case 198730002:
                                phoneCallStatusReason.Value = 924510001;
                                break;

                            default:
                                throw new Exception($"!! Error: Disposition Call Code {callDispositionCode} has not recognized value to map with Phone Call Status reason. Please check");
                        }

                        // Search if there's a phonecall task crated by a sequence (no twilio activity),
                        // on active status related to the same parties.
                        // and if the direction is Outgoing
                        // <condition attribute=""ownerid"" operator=""eq"" value=""{fromId}"" />
                        tracingService.Trace($"toid: {toId}");
                        var fetch = $@"<fetch>
                                        <entity name='phonecall' >
                                            <attribute name='subject' />
                                            <attribute name='scheduledstart' />
                                            <attribute name='scheduledend' />
                                            <attribute name='activityid' />
                                            <attribute name='description' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='regardingobjectid' operator='eq' value='{toId}' />                                              
                                              <condition attribute='stt_fromtwilio' operator='ne' value='1' />
                                              <condition attribute='directioncode' operator='eq' value='1' />
                                            </filter>
                                            <order attribute='createdon' descending='true' />
                                        </entity>
                                    </fetch>";

                        tracingService.Trace("Performing search");

                        var phonecalls = service.RetrieveMultiple(new FetchExpression(fetch));

                        if (phonecalls.Entities.Count > 0)
                        {
                            tracingService.Trace($"Process found {phonecalls.Entities.Count} phone calls that meet criteria.");
                            try
                            {
                                // Loop on the original phonecall found by the query and update info with the twilio one. The twilio phone call will be discarded.
                                foreach (Entity phonecall in phonecalls.Entities)
                                {
                                    tracingService.Trace($"to update:  " + phonecall.Id + " " + phonecall.GetAttributeValue<string>("subject"));

                                    // Map description from twilio phone call into the original phone call.
                                    if (phonecallFromTrigger.ContainsData("description"))
                                    {
                                        if (phonecall.ContainsData("description"))
                                        {
                                            tracingService.Trace("Updating description");
                                            phonecall["description"] = $"{phonecall.GetAttributeValue<string>("description")} " +
                                                                       $"{"\n--------------------\n"} " +
                                                                       $"{phonecallFromTrigger.GetAttributeValue<string>("description")}";
                                        }
                                        else
                                        {
                                            phonecall["description"] = $"{phonecallFromTrigger.GetAttributeValue<string>("description")}";
                                        }
                                    }

                                    if (phonecallFromTrigger.ContainsData("actualstart") && phonecallFromTrigger.ContainsData("actualend"))
                                    {
                                        phonecall["actualstart"] = phonecallFromTrigger.GetAttributeValue<DateTime>("actualstart");
                                        phonecall["actualend"] = phonecallFromTrigger.GetAttributeValue<DateTime>("actualend");
                                        tracingService.Trace($"Total Minutes: {(int)(phonecallFromTrigger.GetAttributeValue<DateTime>("actualend") - phonecallFromTrigger.GetAttributeValue<DateTime>("actualstart")).TotalMinutes} ");

                                        // Do calculation about the duration
                                        phonecall["actualdurationminutes"] = (int)(phonecallFromTrigger.GetAttributeValue<DateTime>("actualend")
                                                                            - phonecallFromTrigger.GetAttributeValue<DateTime>("actualstart")).TotalMinutes;
                                    }

                                    phonecall["stt_recordurl"] = phonecallFromTrigger.GetAttributeValue<string>("stt_recordurl");

                                    // Closing the phone call activity since the contact has been contacted through twilio.
                                    phonecall["stt_calldispositioncode"] = callDispositionCode;
                                    phonecall["statecode"] = new OptionSetValue(1);
                                    phonecall["statuscode"] = phoneCallStatusReason;

                                    // Adding try catch block to wrap any potential exception
                                    try
                                    {
                                        tracingService.Trace($"** Updating {phonecall.Id} original phone call with details from Twilio phone call activity...");
                                        service.Update(phonecall);
                                    }
                                    catch (Exception ex)
                                    {
                                        tracingService.Trace($"!! Error updating phonecall {phonecall.Id}: {ex.Message} - {ex.StackTrace}");
                                        throw;
                                    }
                                }

                                // After updating all open phone calls, delete the twilio phone call activity
                                // Adding try catch block to wrap any potential exception
                                try
                                {
                                    tracingService.Trace($"** Removing phonecall created by Twilio...");
                                    service.Delete("phonecall", phonecallFromTrigger.Id);
                                }
                                catch (Exception ex)
                                {
                                    tracingService.Trace($"!! Error deleting phonecall {phonecallFromTrigger.Id}: {ex.Message} - {ex.StackTrace}");
                                    throw;
                                }
                            }
                            catch (Exception ex)
                            {
                                new Exception("Failed when try to update the phonecall or when try to remove the phonecall created from Twilio.", ex);
                            }
                        }
                        else
                        {
                            tracingService.Trace($"Process didn't find open phone calls. Trying to assign market or returning error to Twilio Flex.");

                            if (phonecallFromTrigger.Contains("ownerid") && phonecallFromTrigger.GetAttributeValue<EntityReference>("ownerid").LogicalName == "systemuser")
                            {
                                tracingService.Trace($"The phone call owner is an user, process continues");

                                // Validate if the phone call owner has one or more market associated
                                tracingService.Trace($"Calling GetMarketFromPhoneCallOwner to retrieve market or throw error either it's present in more than one market or doesn't have market related");
                                EntityReference market = this.GetMarketFromPhoneCallOwner(tracingService, service, phonecallFromTrigger.GetAttributeValue<EntityReference>("ownerid").Id);

                                tracingService.Trace($"Market is not null, assigning it to twilio phone call and closing the phone call as a good call.");

                                // Assign Market to the phone call and close its status as a good call.
                                phonecallFromTrigger["stt_marketid"] = market;
                                phonecallFromTrigger["statecode"] = new OptionSetValue(1);
                                phonecallFromTrigger["statuscode"] = phoneCallStatusReason;
                            }
                            else
                            {
                                tracingService.Trace("!! Error: Phone call doesn't have owner related or the owner is not an User. Please check out that.");
                                throw new Exception("!! Error: Phone call doesn't have owner related. Please check out that.");
                            }

                            // Adding try catch block to wrap any potential exception
                            try
                            {
                                tracingService.Trace($"** Updating phonecall created by Twilio...");
                                service.Update(phonecallFromTrigger);
                            }
                            catch (Exception ex)
                            {
                                tracingService.Trace($"!! Error updating phonecall {phonecallFromTrigger.Id}: {ex.Message} - {ex.StackTrace}");
                                throw;
                            }
                        }
                    }
                    else
                    {
                        // Retry searching the phonecall to see the whether the plugin completed or not
                        Entity phonecall = service.Retrieve("phonecall", phonecallFromTrigger.Id, new ColumnSet("stt_wascreatedbyasequence", "stt_wascreatedmanually"));

                        if (phonecall != null)
                        {
                            if (phonecall.GetAttributeValue<bool>("stt_wascreatedbyasequence") == false && phonecall.GetAttributeValue<bool>("stt_wascreatedmanually") == false)
                            {
                                tracingService.Trace("Phone call was created by Sales Accelerator. Next step, remove it from the system");
                                service.Delete("phonecall", phonecall.Id);
                                tracingService.Trace("Phone call was crated by Sales Accelerator was successfully deleted.");
                            }
                            else
                            {
                                tracingService.Trace("Phone call wasn't created by Sales Accelerator. Then the record stays.");
                            }
                        }
                        else
                        {
                            tracingService.Trace("There was an issue trying to get the phone call activity. Check the whole process with logs");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"!! Error in CreatePostOpPhonecallTwilio plugin: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error in CreatePostOpPhonecallTwilio plugin: {ex.Message}");
            }
        }

        /// <summary>
        /// Method for validating if the phone call owner is present in more than one market or only one.
        /// In order to grab the market and assign it into the phone call record.
        /// </summary>
        /// <param name="tracingService">Tracing service.</param>
        /// <param name="service">IOrganizastion service.</param>
        /// <param name="phonecallOwner">The guid of the user that owns the record in dynamics.</param>
        /// <returns>Returns the Market Entity Reference</returns>
        /// <exception cref="Exception">Exception.</exception>
        public EntityReference GetMarketFromPhoneCallOwner(ITracingService tracingService, IOrganizationService service, Guid phonecallOwner)
        {
            tracingService.Trace("IsThePhoneCallOwnerInMultipleMarkets method was invoked.");
            tracingService.Trace($"Search if the phone call owner: {phonecallOwner} is in multiple markets as ISA at same time");

            var query = $@"<fetch>
                                <entity name='stt_marketteam'>
	                                <attribute name='stt_marketteamid' />
		                            <attribute name='stt_marketid' />
		                            <attribute name='stt_marketidname' />
		                            <filter type='and'>
			                            <condition attribute='statecode' operator='eq' value='0' />                                           
                                        <condition attribute='stt_isaid' operator='eq' value='{phonecallOwner}' />
		                            </filter>
	                            </entity>
                            </fetch>";

            var teamMarkets = service.RetrieveMultiple(new FetchExpression(query));
            tracingService.Trace($"teamMarkets Count: {teamMarkets.Entities.Count}");

            if (teamMarkets.Entities.Count > 0)
            {
                if (teamMarkets.Entities.Count == 1)
                {
                    if (teamMarkets.Entities[0].ContainsData("stt_marketid"))
                    {
                        tracingService.Trace($"market Found: {teamMarkets.Entities[0].GetAttributeValue<EntityReference>("stt_marketid").Id}");
                        return teamMarkets.Entities[0].GetAttributeValue<EntityReference>("stt_marketid");
                    }
                    else
                    {
                        throw new Exception("The Team market doesn't have Market value. Fix the data before continue.");
                    }
                }
                else
                {
                    throw new Exception("The phone call owner is present in more than one market. Please create/update the record manually to assign a valid market.");
                }
            }
            else
            {
                throw new Exception("The phone call owner doesn't have any market assigned at Team Market table.");
            }
        }
    }
}
