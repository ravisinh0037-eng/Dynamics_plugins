// <copyright file="CreatePreOpEmailBodyCleanup.cs" company="VEER">
// Copyright 2025 VEER
// </copyright>

namespace VEER.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using VEER.Plugins.Models;

    public class CreatePreOpEmailBodyCleanup : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin - Start");

            try
            {
                if (context.Depth > 1)
                {
                    tracingService.Trace($"Depth: {context.Depth}");
                    return;
                }

                tracingService.Trace($"Depth: {context.Depth}");

                Entity emailEntity = null;
                bool updateRequired = false;

                // Create handler
                if (context.MessageName == "Create" || context.MessageName == "Update")
                {
                    if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity email)
                    {
                        tracingService.Trace("Target email found for Create.");
                        if (context.MessageName == "Update")
                        {
                            emailEntity = email;

                            tracingService.Trace("Target email found for Update.");
                        }
                        else
                        {
                            emailEntity = email;

                            tracingService.Trace("Target email found for Create.");
                        }

                        updateRequired = false;
                    }
                }

                // DeliverIncoming / DeliverPromote handler
                else if (context.MessageName == "DeliverIncoming" || context.MessageName == "DeliverPromote")
                {
                    tracingService.Trace("Loading email using PrimaryEntityId for DeliverIncoming/DeliverPromote.");
                    Guid emailId = context.PrimaryEntityId;

                    if (emailId == Guid.Empty)
                    {
                        tracingService.Trace($"emailID from context primary entity is 00000000-0000-0000-0000-000000000000 {emailId}");
                        return;
                    }

                    tracingService.Trace($"EmailId: {emailId}");
                    emailEntity = service.Retrieve(Emails.LogicalName, emailId, new ColumnSet(Emails.Description, Emails.Status));
                    updateRequired = true;
                }

                if (emailEntity == null)
                {
                    tracingService.Trace("Email entity not found. Plugin exiting.");
                    return;
                }

                tracingService.Trace($"Processing entity: {emailEntity.LogicalName}");
                var emailStatus = emailEntity.GetAttributeValue<OptionSetValue>(Emails.Status);

                if (emailStatus == null && emailEntity.Id != Guid.Empty)
                {
                    var existingEmail = service.Retrieve(Emails.LogicalName, emailEntity.Id, new ColumnSet(Emails.Status));

                    emailStatus = existingEmail.GetAttributeValue<OptionSetValue>(Emails.Status);
                }

                if (emailStatus == null)
                {
                    tracingService.Trace("Email status is not present. Plugin exiting.");
                    return;
                }

                tracingService.Trace($"emailStatus: {emailStatus.Value}");

                if (emailStatus.Value != 3 && emailStatus.Value != 2 && emailStatus.Value != 4)
                {
                    tracingService.Trace("the email status is either not present or not sent/received/completed.");
                    return;
                }

                // Clean description
                this.Implementation(emailEntity, tracingService);

                // If retrieving the email from DB, update it
                if (updateRequired)
                {
                    tracingService.Trace("Updating email description...");
                    service.Update(emailEntity);
                }

                // Delete inline image attachments
                if (context.PrimaryEntityId != Guid.Empty)
                {
                    tracingService.Trace("Deleting inline image attachments...");
                    this.DeleteInlineImageAttachments(context.PrimaryEntityId, service, tracingService);
                }
            }
            catch (FaultException<OrganizationServiceFault> fex)
            {
                tracingService.Trace("Error: " + fex.ToString());
                throw new InvalidPluginExecutionException($"Error in {this.GetType().FullName}: {fex.Detail.Message}", fex);
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error: " + ex.ToString());
                throw new InvalidPluginExecutionException($"Error in {this.GetType().FullName}: {ex.Message}", ex);
            }

            tracingService.Trace("Plugin - End");
        }

        private void Implementation(Entity email, ITracingService tracingService)
        {
            tracingService.Trace($"Start HTML cleanup implementation.Email id: {email.Id}");

            if (!email.Contains(Emails.Description) || email[Emails.Description] == null)
            {
                tracingService.Trace("Email description is empty. Skipping.");
                return;
            }

            string html = email[Emails.Description].ToString();

            if (string.IsNullOrWhiteSpace(html))
            {
                tracingService.Trace("Email description is whitespace. Skipping.");
                return;
            }

            tracingService.Trace($"Original HTML length: {html.Length}");

            bool hasSignatureDiv = Regex.IsMatch(html, @"<div[^>]*id\s*=\s*[""']Signature[""']", RegexOptions.IgnoreCase);

            tracingService.Trace($"Contains Signature div: {hasSignatureDiv}");

            bool hasStewartLogo = html.IndexOf("stewart-title-pms.gif", StringComparison.OrdinalIgnoreCase) >= 0;

            tracingService.Trace($"Contains Stewart logo URL: {hasStewartLogo}");

            int imageTagCount = Regex.Matches(html, @"<\s*img\b", RegexOptions.IgnoreCase).Count;

            tracingService.Trace($"IMG tag count BEFORE cleanup: {imageTagCount}");

            var imgMatches = Regex.Matches(html, @"<\s*img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in imgMatches)
            {
                string imgTag = match.Value;

                var srcMatch = Regex.Match(imgTag, @"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);

                if (srcMatch.Success)
                {
                    tracingService.Trace($"IMG SRC: {srcMatch.Groups[1].Value}");
                }
            }

            // html = Regex.Replace(html, "<img[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // html = Regex.Replace(html, "&lt;img[^&gt;]*&gt;", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // html = Regex.Replace(html, "&amp;lt;img[^&amp;gt;]*&amp;gt;", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Remove base64 inline images
            // html = Regex.Replace(html, "data:image\\/[^;]+;base64,[^\"]+", string.Empty, RegexOptions.IgnoreCase);
            // Remove cid: references
            // html = Regex.Replace(html,"cid:[^\"'>\\s]+",string.Empty,RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // (Optional) clean empty p tags, VML, comments etc.
            // html = Regex.Replace(html,"&lt;p[^&gt;]*&gt;(\\s|&amp;nbsp;|&lt;br\\s*/?&gt;|&lt;o:p&gt;.*?&lt;/o:p&gt;)*&lt;/p&gt;",string.Empty,RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Replace all normal <img> tags with a small placeholder image
            html = Regex.Replace(html, @"<\s*img\b[^>]*>", @"<img src=""cid:removed-image-placeholder"" width=""16"" height=""16"" style=""display:inline-block;"" />", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"&lt;\s*img\b[^&]*?&gt;", @"<img src=""cid:removed-image-placeholder"" width=""16"" height=""16"" style=""display:inline-block;"" />", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"&amp;lt;\s*img\b[^&]*?&amp;gt;", @"<img src=""cid:removed-image-placeholder"" width=""16"" height=""16"" style=""display:inline-block;"" />", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"data:image\/[^;]+;base64,[^""'\s>]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"&lt;p[^&gt;]*&gt;(\s|&amp;nbsp;|&lt;br\s*/?&gt;|&lt;o:p&gt;.*?&lt;/o:p&gt;)*&lt;/p&gt;", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            tracingService.Trace($"Cleaned HTML length: {html.Length}");

            bool signatureStillExists = Regex.IsMatch(html, @"<div[^>]*id\s*=\s*[""']Signature[""']", RegexOptions.IgnoreCase);

            tracingService.Trace($"Signature div still exists: {signatureStillExists}");

            bool logoStillExists = html.IndexOf("stewart-title-pms.gif", StringComparison.OrdinalIgnoreCase) >= 0;

            tracingService.Trace($"Stewart logo still exists: {logoStillExists}");

            int imagesAfter = Regex.Matches(html, @"<\s*img\b", RegexOptions.IgnoreCase).Count;

            tracingService.Trace($"IMG tag count AFTER cleanup: {imagesAfter}");
            email[Emails.Description] = html;
            var stripped = Regex.Replace(html, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            stripped = Regex.Replace(stripped, "<.*?>", string.Empty, RegexOptions.Singleline);
            stripped = stripped.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'");
            stripped = Regex.Replace(stripped, @"[ \t]+", " ");

            stripped = Regex.Replace(stripped, @"\r?\n\s*\r?\n+", Environment.NewLine + Environment.NewLine);

            stripped = stripped.Trim();

            // email[Emails.Description] = stripped;
            tracingService.Trace("End of Implementation");
        }

        private void DeleteInlineImageAttachments(Guid emailId, IOrganizationService service, ITracingService tracing)
        {
            tracing.Trace("Searching for inline image attachments...");

            QueryExpression qe = new QueryExpression("activitymimeattachment");
            qe.ColumnSet = new ColumnSet("activitymimeattachmentid", "mimetype", "objectid", "filesize");
            qe.Criteria.AddCondition("objectid", ConditionOperator.Equal, emailId);

            // qe.Criteria.AddCondition("mimetype", ConditionOperator.Like, "image/%");
            var attachments = service.RetrieveMultiple(qe);
            int imageCount = 0;
            int otherAttachmentCount = 0;

            foreach (var att in attachments.Entities)
            {
                var mimeType = att.GetAttributeValue<string>("mimetype") ?? string.Empty;
                var fileSize = att.GetAttributeValue<int?>("filesize");
                tracing.Trace($"Attachment: {att.Id}, " + $"MIME: {mimeType}, " + $"FileSize: {fileSize}");
                bool isImage = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                if (isImage)
                {
                    imageCount++;
                    tracing.Trace("Deleting attachment: " + att.Id);
                    service.Delete("activitymimeattachment", att.Id);
                }
                else
                {
                    otherAttachmentCount++;
                    tracing.Trace("Clearing body for attachment: " + att.Id + " (mimetype: " + mimeType + ")");
                    Entity updateAttachment = new Entity("activitymimeattachment", att.Id);
                    updateAttachment["body"] = null;
                    service.Update(updateAttachment);
                }
            }

            tracing.Trace($"Attachment summary: " + $"Images={imageCount}, " + $"Other Attachments={otherAttachmentCount}, " + $"Total={attachments.Entities.Count}");
            tracing.Trace("Attachment processing completed.");
        }
    }
}
