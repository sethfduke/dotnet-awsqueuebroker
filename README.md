# AwsQueueBroker

The AwsQueueBroker library allows for SQS queue message definitions to be constructed by assigning unique name attributes on each message and attaching messages by that name to an implementation of a message processor class and it's associated .net object model. This allows the developer to remove all the logic of receiving, sending, and deleting SQS queue messages from their code-base so that the focus can be placed on the actual processing of messages in a structured and repeatable manner.

## Installation

The library is available via Nuget at https://www.nuget.org/packages/AwsQueueBroker

## Usage

Todo

## Issues

Please submit any issues or feature requests to https://github.com/sethfduke/dotnet-awsqueuebroker/issues

## Contributing

1. Fork this repository.
2. Create your feature branch: `git checkout -b my-new-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin my-new-feature`
5. Submit a pull request

## History

Version 1.0.0 (2021-02-16) - Initial release

## Credits

Seth Duke (@sethfduke)

## License

The MIT License (MIT)

Copyright (c) 2021 Seth Duke

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.