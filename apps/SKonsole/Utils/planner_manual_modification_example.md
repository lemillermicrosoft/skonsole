[note]: # (Manual Plan Modification)
> dotnet run createplan "Find news about technology and write a funny poem about it."
<goal>
Find news about technology and write a funny poem about it.
</goal>
<plan>
  <function.bing.SearchAsync input="technology news" setContextVariable="SEARCH_RESULT" />
  <function._GLOBAL_FUNCTIONS_.BucketOutputs input="$SEARCH_RESULT" bucketCount="3" bucketLabelPrefix="NEWS" />
  <function.WriterPlugin.ShortPoem input="$NEWS_1; $NEWS_2; $NEWS_3" appendToResult="RESULT__POEM" />
</plan>

[note]: # (I don't like that the plan uses ShortPoem, so I will manually modify it and execute the new plan. Additionally, I want it to just use the first 2 headlines.)
<goal>
Find news about technology and write a funny poem about it.
</goal>
<plan>
  <function.bing.SearchAsync input="technology news" setContextVariable="SEARCH_RESULT" />
  <function._GLOBAL_FUNCTIONS_.BucketOutputs input="$SEARCH_RESULT" bucketCount="3" bucketLabelPrefix="NEWS" />
  <function.WriterPlugin.LongPoem input="$NEWS_1; $NEWS_2" appendToResult="RESULT__POEM" />
</plan>

[note]: # (Execute the manually modified plan)
There once was a man named Musk
Who had a penchant for futuristic stuff
He bored some tunnels in Vegas for fun
And filled them with Teslas that could run
But the drivers soon found out they were in a rut

For the tunnels were narrow and dark
And the Teslas had no place to park
They zoomed along at a snail's pace
With no room to swerve or race
And the passengers wished they had taken a lark

Meanwhile, a pub in the UK
Had a brilliant idea one day
They installed a device on the door
That measured the breath of the boor
And locked them in if they were too drunk to pay

The device was called Alco-Lock
And it caused quite a shock
For the drinkers who wanted to leave
But had to wait till they could breathe
Or sober up with some coffee or mock

The pub soon became a sensation
And a model for road safety education
For the Alco-Lock reduced the crashes
And the drunk drivers' lashes
And the pub made a fortune from donations