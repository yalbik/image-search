# Sample Images Folder

This folder should contain your image files for indexing and searching.

## Supported Formats
- .jpg / .jpeg
- .png

## Recommendations
- Use images smaller than 2MB for better performance
- Clear, well-lit photos work best with LLaVA
- Descriptive content (people, objects, scenes) provides better search results

## Example Organization
```
my_images/
├── nature/
│   ├── sunset.jpg
│   ├── forest.png
│   └── mountains.jpg
├── people/
│   ├── family.jpg
│   └── friends.png
└── objects/
    ├── cars.jpg
    └── furniture.png
```

## Getting Started
1. Add some sample images to this folder
2. Run the application: `cd OllamaRedisImageSearch && dotnet run`
3. Choose option 1 to index your images
4. Choose option 2 to search with natural language queries

## Example Queries
- "people smiling"
- "red cars"
- "sunset landscape"
- "dogs playing"
- "books on a table"
