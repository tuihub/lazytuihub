# LazyTuiHub API

> Generated from Proto declarations. Run `mise -E development run generate:api` to update; do not edit `docs/api`.

**API version:** `v1`

## Operations

- [GET /v1/articles](operations/ArticleService_ListArticles.md)
- [GET /v1/articles/{article}](operations/ArticleService_GetArticle.md)
- [POST /v1/articles/{article}:markRead](operations/ArticleService_MarkArticleRead.md)
- [GET /v1/feeds](operations/RssService_ListFeeds.md)
- [POST /v1/feeds](operations/RssService_CreateFeed.md)
- [GET /v1/feeds/{feed}](operations/RssService_GetFeed.md)
- [POST /v1/feeds/{feed}:refresh](operations/RssService_RefreshFeed.md)
- [POST /v1/sessions:authenticateUser](operations/SessionService_AuthenticateUser.md)
- [POST /v1/sessions:revokeSession](operations/SessionService_RevokeSession.md)
- [GET /v1:describeWorker](operations/WorkerService_DescribeWorker.md)
- [POST /v1:executePacket](operations/WorkerService_ExecutePacket.md)
- [POST /v1:generateGreeting](operations/GreetingService_GenerateGreeting.md)

## Models

- [Article](models/Article.md)
- [AuthenticateUserRequest](models/AuthenticateUserRequest.md)
- [AuthenticateUserResponse](models/AuthenticateUserResponse.md)
- [DescribeWorkerResponse](models/DescribeWorkerResponse.md)
- [ExecutePacketRequest](models/ExecutePacketRequest.md)
- [ExecutePacketResponse](models/ExecutePacketResponse.md)
- [Feed](models/Feed.md)
- [GenerateGreetingRequest](models/GenerateGreetingRequest.md)
- [GenerateGreetingResponse](models/GenerateGreetingResponse.md)
- [ListArticlesResponse](models/ListArticlesResponse.md)
- [ListFeedsResponse](models/ListFeedsResponse.md)
- [MarkArticleReadRequest](models/MarkArticleReadRequest.md)
- [RefreshFeedRequest](models/RefreshFeedRequest.md)
- [RefreshFeedResponse](models/RefreshFeedResponse.md)
- [RevokeSessionRequest](models/RevokeSessionRequest.md)
- [RevokeSessionResponse](models/RevokeSessionResponse.md)

## Source declarations

- [lazytuihub/access/v1/session_service.proto](../../src/LazyTuiHub.Api/Protos/lazytuihub/access/v1/session_service.proto)
- [lazytuihub/articles/v1/article.proto](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/contracts/Protos/lazytuihub/articles/v1/article.proto)
- [lazytuihub/articles/v1/article_service.proto](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/contracts/Protos/lazytuihub/articles/v1/article_service.proto)
- [lazytuihub/execution/v1/worker_service.proto](../../src/LazyTuiHub.Api/Protos/lazytuihub/execution/v1/worker_service.proto)
- [lazytuihub/greeting/v1/greeting_service.proto](../../src/LazyTuiHub.Api/Protos/lazytuihub/greeting/v1/greeting_service.proto)
- [lazytuihub/rss/v1/feed.proto](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/contracts/Protos/lazytuihub/rss/v1/feed.proto)
- [lazytuihub/rss/v1/rss_service.proto](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/contracts/Protos/lazytuihub/rss/v1/rss_service.proto)
