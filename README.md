# KotobaQuizzes

Creates quizzes for [Kotoba](https://kotobaweb.com/) by rotating each character to mess with your head for fun.
Note that you currently need more than 1000 points to upload an image quiz.

[.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) is required to run this project.

## Usage

```
Usage:
  KotobaQuizzes [options]

Options:
  --input <input> (REQUIRED)                Kotoba bot CSV file to base quiz on.
  --font <font> (REQUIRED)                  The font to use.
  --url-template <url-template> (REQUIRED)  The template to use for the Question URLs. Use {0} for the filename.
  --font-size <font-size>                   The font size to draw with. [default: 400]
  --result <result>                         The directory to place the resulting images and json file in. Default is ./result. []
  --delete-result                           Whether or not to delete the result directory. [default: True]
  --instructions <instructions>             The instructions to set to each question. If this option is not set or is set to "null" instructions will be wiped. []
  --rotate <rotate>                         How many degrees to rotate each character by. If not set or set to a negative number rotation will be random. [default: -1]
  --cpus <cpus>                             The number of threads to use for generating images. By default your number of cores is used. [default: 12]
  --generate-images                         [default: True]
  --version                                 Show version information
  -?, -h, --help                            Show help and usage information
```
