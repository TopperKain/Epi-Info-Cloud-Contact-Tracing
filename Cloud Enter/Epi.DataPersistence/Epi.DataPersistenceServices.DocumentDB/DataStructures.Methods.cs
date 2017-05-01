﻿using System;
using System.Collections.Generic;
using System.Linq;
using Epi.Common.Core.DataStructures;
using Epi.Common.Core.Interfaces;
using Epi.DataPersistence.Constants;
using Microsoft.Azure.Documents;

namespace Epi.PersistenceServices.DocumentDB
{
    public partial class FormResponseResource
    {
        public FormResponseProperties AddOrReplaceChildResponse(FormResponseProperties childResponse)
        {
            var parentResponseId = childResponse.ParentResponseId;
            var childFormName = childResponse.FormName;
            var childResponseId = childResponse.ResponseId;

            var childResponseList = GetChildResponseList(parentResponseId, childFormName, /*addIfNoList=*/true);
            var existingResponse = childResponseList.SingleOrDefault(r => r.ResponseId == childResponseId);
            var index = childResponseList.FindIndex(r => r.ResponseId == childResponseId);
            if (index >= 0)
            {
                childResponseList.RemoveAt(index);
            }
            childResponseList.Add(childResponse);

            // Add the response to the response index if it doesn't alread exist.
            if (index < 0)
            {
                ChildResponseIndex.Add(childResponse.ResponseId, new ResponseDirectory(childResponse));
            }

            return childResponse;
        }

        public List<FormResponseProperties> GetChildResponseList(IResponseContext responseContext, bool addIfNoList = false)
        {
            return GetChildResponseList(responseContext.ParentResponseId, responseContext.FormName);
        }

        public List<FormResponseProperties> GetChildResponseList(string parentResponseId, string childFormName, bool addIfNoList = false)
        {
            Dictionary<string/*ChildFormId*/, List<FormResponseProperties>> childResponsesByChildFormId = null;
            childResponsesByChildFormId = (ChildResponses.TryGetValue(parentResponseId, out childResponsesByChildFormId)) ? childResponsesByChildFormId : null;
            if (childResponsesByChildFormId == null && addIfNoList)
            {
                ChildResponses.Add(parentResponseId, childResponsesByChildFormId = new Dictionary<string/*ChildFormId*/, List<FormResponseProperties>>());
            }

            List<FormResponseProperties> childResponseList = null;
            if (childResponsesByChildFormId != null)
            {
                childResponseList = (childResponsesByChildFormId.TryGetValue(childFormName, out childResponseList)) ? childResponseList : null;
                if (childResponseList == null && addIfNoList)
                {
                    childResponsesByChildFormId.Add(childFormName, childResponseList = new List<FormResponseProperties>());
                }
            }
            return childResponseList;
        }

        public FormResponseProperties GetChildResponse(IResponseContext responseContext)
        {
            return GetChildResponse(responseContext.ParentResponseId, responseContext.FormName, responseContext.ResponseId);
        }

        public FormResponseProperties GetChildResponse(string parentResponseId, string childFormName, string childResponseId)
        {
            var childResponseList = GetChildResponseList(parentResponseId, childFormName);
            var childResponse = childResponseList != null ? childResponseList.SingleOrDefault(r => r.ResponseId == childResponseId) : null;
            return childResponse;
        }

        public void LogicalCascadeDelete(FormResponseProperties formResponseProperties)
        {
            CascadeThroughChildren(formResponseProperties.ResponseId, frp => frp.RecStatus = RecordStatus.Deleted);
        }

        public void CascadeThroughChildren(string parentResponseId, Action<FormResponseProperties> action)
        {
            Dictionary<string/*ChildFormName*/, List<FormResponseProperties>> children = null;
            if (ChildResponses.TryGetValue(parentResponseId, out children))
            {
                // interate over list child forms
                foreach (var childFormId_Dictionary in ChildResponses.Values)
                {
                    // iterate over list of child responses
                    foreach (var formResponsePropertiesList in childFormId_Dictionary.Values)
                    {
                        foreach (var formResponseProperties in formResponsePropertiesList)
                        {
                            action(formResponseProperties);
                            CascadeThroughChildren(formResponseProperties.ResponseId, action);
                        }
                    }
                }
            }
        }
    }
}