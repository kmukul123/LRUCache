# LRUCache

Simple implementation of LRUCache in .Net and basic test cases for that.
it uses a linkedlist to arrange the items in the order of last usage
the oldest unused element is at the end and the newest element is at top
The implementation is threadsafe and locks only the minimal nodes of linkedlist for higher performance

#comments
we can improve the tests and have unit tests for method exposed by cacheList
There are other todo comments for further working

THe code coverage results are in .coveragexml file at the root level