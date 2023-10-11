[note]: # (Natural Language Iteration)
dotnet run createplan "Find the latest news headlines and write a funny poem about it."
<goal>
Find the latest news headlines and write a funny poem about it.
</goal>
<plan>
  <function.bing.SearchAsync input="latest news headlines" setContextVariable="SEARCH_RESULT" />
  <function._GLOBAL_FUNCTIONS_.BucketOutputs input="$SEARCH_RESULT" bucketCount="3" bucketLabelPrefix="HEADLINE" />
  <function.WriterPlugin.funce0b2aafaf46c4761bb7d0cb34e0402f7 input="$HEADLINE_1; $HEADLINE_2; $HEADLINE_3" appendToResult="RESULT__POEM" />
</plan>

[note]: # (Did not like that it passed all three headlines to WriterPlugin, so will tweak my ask.)
dotnet run createplan "Find the latest news headlines and write a funny poem about each of them."
<goal>
Find the latest news headlines and write a funny poem about each of them.
</goal>
<plan>
  <function.bing.SearchAsync input="latest news headlines" appendToResult="RESULT__HEADLINES" />
  <function._GLOBAL_FUNCTIONS_.BucketOutputs input="$RESULT__HEADLINES" bucketCount="3" bucketLabelPrefix="Headline" />
  <function.WriterPlugin.funcc033d61295e140d0b9713523add0f1a9 input="$Headline_1" appendToResult="RESULT__POEM_1" />
  <function.WriterPlugin.funcc033d61295e140d0b9713523add0f1a9 input="$Headline_2" appendToResult="RESULT__POEM_2" />
  <function.WriterPlugin.funcc033d61295e140d0b9713523add0f1a9 input="$Headline_3" appendToResult="RESULT__POEM_3" />
</plan>

[note]: # (Success, I'm confident this will now generate three different poems about each of the headlines the bing plugin Search function returns. Execute the plan.)
RESULT__POEM_1

There once was a website called Google
That offered the users a frugal
Way to search for the news
And get various views
But sometimes the headlines were muggle

They showed them more top stories galore
But some of them were quite a bore
Like how to bake bread
Or who recently wed
Or what was the latest fashion store

So some users decided to rebel
And searched for more interesting tales
Like how to fight crime
Or travel through time
Or what were the secrets of the whales

But Google was not very pleased
And tried to make them change their keys
They said, "Don't be silly
You'll miss something really
Important, like the cure for fleas"

RESULT__POEM_2

There once was a host named Tucker
Who wanted to see the Capitol ruckus
He asked McCarthy to show him the tapes
Of the rioters in their MAGA capes

McCarthy agreed to grant his request
He thought Tucker was one of the best
But when they watched the footage together
They saw something that made them shudder

It was McCarthy himself in the crowd
Shouting and pushing and acting proud
He tried to explain to Tucker his role
But Tucker just laughed and called him a troll

RESULT__POEM_3

The EPA chief was in a hurry
To visit Ohio and calm the worry
Of people who drank from a toxic well
And felt quite ill, as they could tell

He packed his bags and took a flight
But something on the plane wasn't right
He started to cough and sneeze and wheeze
And his skin turned green with a strange disease

He landed in Ohio with a fright
And rushed to the nearest hospital site
But the doctors there were shocked to see
That he had grown a third eye and a tail of a bee

They ran some tests and found the cause
He had been exposed to a chemical sauce
That leaked from a barrel in the cargo hold
And mixed with his water, so he was told

The chemical was a banned pesticide
That the EPA itself had tried to hide
But someone had smuggled it on the plane
To sell it in Ohio for a hefty gain

The EPA chief was in a mess
He had to confess and face the press
But he couldn't find the words to say
How he became a mutant in a day

He decided to resign and leave the post
And find a place where he could coast
He moved to a farm with a friendly goat
And wrote some poems and a memoir note