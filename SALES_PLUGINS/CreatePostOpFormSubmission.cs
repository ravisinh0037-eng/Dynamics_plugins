// <copyright file="CreatePostOpPhonecallTwilio.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.IdentityModel.Metadata;
    using System.Threading;
    using VEER.Plugins.Utils;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;

    /// <summary>
    /// DEPRECATED
    /// When a Form Submission is created.
    /// </summary>
    /// <remarks>
    /// This plugin needs to be registered for:
    ///
    /// Message: "Create" - Primary Entity: "FormSubmission" - Secondary Entity: n/a
    ///     Run as: Calling User - Execution Order: 1
    ///     Stage: Post Operation - Execution Mode: Synchronous - Deploy; Server.
    ///
    /// </remarks>
    public class CreatePostOpFormSubmission : IPlugin
    {
        /// <inheritdoc/>
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace($"Starting CreatePostOpFormSubmission plugin... ");

            try
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity)
                {
                    tracingService.Trace($"Entity: {entity.LogicalName}");
                    if (entity.LogicalName == "msdynmkt_marketingformsubmission" && context.MessageName.ToLower() == "create")
                    {
                        var formSubmissionId = entity.Id;
                        tracingService.Trace($"Form Submission Id: {formSubmissionId}");

                        var fetch = $@"<fetch>
                                          <entity name=""msdynmkt_marketingformsubmission"">
                                            <attribute name=""msdynmkt_marketingformsubmissionid"" />
                                            <attribute name=""msdynmkt_submittedvalues"" />
                                            <attribute name=""msdynmkt_eventregistration"" />
                                            <attribute name=""msdynmkt_contactableemail"" />
                                            <attribute name=""msdynmkt_pageurl"" />
                                            <filter>
                                              <condition attribute=""msdynmkt_marketingformsubmissionid"" operator=""eq"" value=""{formSubmissionId}"" />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        var formSubmission = service.RetrieveMultiple(new FetchExpression(fetch));

                        if (formSubmission.Entities.Count > 0)
                        {
                            tracingService.Trace($"formEvent.Entities[0]: {formSubmission.Entities[0]}");
                            try
                            {
                                tracingService.Trace($"Inside try...");
                                var urlEvent = formSubmission.Entities[0].GetAttributeValue<string>("msdynmkt_pageurl");

                                tracingService.Trace($"Form submission msdynmkt_pageurl: {urlEvent}");
                                tracingService.Trace($"Form submission msdynmkt_marketingformsubmissionid: {formSubmission.Entities[0].GetAttributeValue<Guid>("msdynmkt_marketingformsubmissionid")}");
                                tracingService.Trace($"Form submission msdynmkt_contactableemail:");
                                tracingService.Trace($"Form submission msdynmkt_submittedvalues: {formSubmission.Entities[0].GetAttributeValue<string>("msdynmkt_submittedvalues")}");

                                var fetchEvent = $@"<fetch >
                                                      <entity name=""msevtmgt_event"">
                                                        <attribute name=""stt_typecode"" />
                                                        <attribute name=""msevtmgt_name"" />
                                                        <attribute name=""msevtmgt_publiceventurl"" />
                                                        <filter>
                                                          <condition attribute=""msevtmgt_publiceventurl"" operator=""eq"" value=""{urlEvent}"" />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                                var formEvent = service.RetrieveMultiple(new FetchExpression(fetchEvent));
                                tracingService.Trace($"formEvent.Entities.Count: { formEvent.Entities.Count}");

                                if (formEvent.Entities.Count > 0)
                                {
                                    tracingService.Trace($"msevtmgt_publiceventurl: { formEvent.Entities[0].GetAttributeValue<string>("msevtmgt_publiceventurl")}");
                                    tracingService.Trace($"msevtmgt_name: { formEvent.Entities[0].GetAttributeValue<string>("msevtmgt_name")}");
                                    var eventType = formEvent.Entities[0].GetAttributeValue<OptionSetValue>("stt_typecode").Value;
                                    tracingService.Trace($"eventType value: {eventType}");

                                    // If eventType = On-Demand Recording
                                    if (eventType == 198730003)
                                    {
                                        tracingService.Trace($"eventType is On-Demand Recording");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                new Exception("Failed in CreatePostOpFormSubmission.", ex);
                            }
                        }
                        else
                        {
                            tracingService.Trace($"Error in CreatePostOpFormSubmission plugin. No entities related");
                            throw new InvalidPluginExecutionException($"Error in CreatePostOpFormSubmission plugin. No entities related");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error in CreatePostOpFormSubmission plugin: {ex.ToString()}");
                throw new InvalidPluginExecutionException($"Error in CreatePostOpFormSubmission plugin: {ex.ToString()}");
            }
        }
    }
}
