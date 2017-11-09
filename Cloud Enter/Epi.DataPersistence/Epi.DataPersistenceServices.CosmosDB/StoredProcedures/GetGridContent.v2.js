﻿function getGridContent(query, sortKey, isSortAscending, pageNumber, responsesPerPage, querySetToken) {
    var context = getContext();
    var response = context.getResponse();
    var collection = context.getCollection();
    var collectionLink = collection.getSelfLink();
    var responses = [];
    var continuationToken = "";
    var currentContinuationToken;
    var responseSize = 0;
    //var isMaxSizeReached = false;
    var numberOfQueries = 0;

    var maxResponseSize = 1024 * 1024;
    //var maxResponseSize = 1000;

    if (!sortKey) {
        sortkey = "_DateCreated";
        isSortAscending = false;
    }

    // Use for debugging in portal
    //sortKey="patientname1";
    //isSortAscending=true;
    //pageNumber = 1;
    //responsesPerPage = 20;
    //skip = 0;

    var trace = "query:" + query
        //+ ", continuationToken:" + continuationToken
        //+ ", skip:" + skip
        + ", sortKey:" + sortKey
        + ", isSortAscending:" + isSortAscending;

    getNodes(continuationToken/*, skip*/);

    // continuationToken identifies the continuation point.
    // skip is then number of documents that were already
    // returned from the previous continuation point.
    function getNodes(continuationToken/*, skip*/) {
        trace += ", getNodes";

        // Tune the pageSize to fit your dataset.
        var requestOptions =
        {
            continuation: continuationToken,
            pageSize: 500
    };

        // The number of documents taken from the current continuation block
        var taken = 0;

        var accepted = collection.queryDocuments(collectionLink, query, requestOptions,
          function (err, documentsRead, responseOptions) {
              trace += ",queryDocments";
              currentContinuationToken = requestOptions.continuation;
              for(var thisResponse of documentsRead)
              {
                  //if (skip > 0) {
                  //    skip -= 1;
                  //}
                  //else {
                      // The size of the current query response page.
                      var thisResponseSize = JSON.stringify(thisResponse).length;

                      // DocumentDB has a response size limit of 1 MB.
                      if (pageNumber > 0 || responseSize + thisResponseSize < maxResponseSize) {
                          // Append response to responses.
                          responses = responses.concat(thisResponse);

                          // Keep track of the total response size.
                          responseSize += thisResponseSize;
                          taken += 1;
                      }
                      else {
                          isMaxSizeReached = true;
                          break;
                      }
                  //}
              }
              trace += ",taken:" + taken;

              if (responseOptions.continuation) {
                 getNodes(responseOptions.continuation);
              }
              // If there is no continutation token, we are done. Return the response.
              responses = schwartzianSort(responses, sortKey);
              var numberOfPages;
              var pageResponses;
              if (pageNumber > 0 && responsesPerPage > 0) {
                  numberOfPages = Math.ceil(responses.length / responsesPerPage);
                  var first = pageNumber * responsesPerPage - responsesPerPage;
                  var last = pageNumber * responsesPerPage;
                  pageResponses = responses.slice(first, last);
              }
              pageNumber = pageNumber ? pageNumber : 0
              response.setBody({
                  "result": pageResponses ? pageResponses : responses,
                  "querySetToken": pageNumber <= 1 ? maxDateValue(responses) : querySetToken,
                  "sortKey": sortKey,
                  "pageNumber": pageNumber,
                  "numberOfPages": numberOfPages ? numberOfPages : 0,
                  "numberOfResponsesReturnedByQuery": responses.length,
                  "numberOfResponsesPerPage": responsesPerPage,
                  "numberOfResponsesOnSelectedPage": pageResponses.length,
                  "message": "Completed",
                  "trace": trace
              });
          });

        if (!accepted) {
            // If the execution limit reached; run the script again with the nextContinuationToken as a script parameter.
            response.setBody({
                "message": "Execution limit reached.",
                //"continuationToken": nextContinuationToken,
                "result": responses,
                "skip": taken
            });
        }
    }

    function maxDateValue(list) {
        var len = list.length
        var max = "0000";
        while (len--) {
            if (list[len].FirstSaveTime > max) {
                max = list[len].FirstSaveTime;
            }
        }
        return max;
    }

    var schwartzianSort = (function () {
        var decorate = function (sortKey) {
            return function (item) {
                switch (sortKey) {
                    case "_UserEmail":
                        return [item["UserName"](), item];
                    case "IsDraftMode":
                    case "_Mode":
                        return [item.IsDraftMode, item];
                    case "_DateCreated":
                        return [item.FirstSaveTime, item];
                    case "_DateUpdated":
                        return [item["_ts"](), item];
                        //return [item.LastSaveTime, item];
                    default:
                        return [item.ResponseQA[sortKey] ? item.ResponseQA[sortKey].toUpperCase() : "", item];
                }
            };
        };

        var compare = function (sortFunction) {
            sortFunction = sortFunction || defaultSortFunction;

            return function (a, b) {
                return sortFunction(a[0], b[0]);
            };
        };

        var defaultSortFunction = function (a, b) {
            if (isSortAscending) {
                if (a < b) return -1;
                if (a > b) return 1;
            }
            else {
                if (a < b) return 1; 0
                if (a > b) return -1;
            }
            return 0;
        };

        var undecorate = function (item) {
            return item[1];
        };

        return function (items, sortKey, sortFunction) {
            return items.map(decorate(sortKey))
						.sort(compare(sortFunction))
						.map(undecorate);
        };
    })();
}