# PYTHON R.S.

# RECOMMENDER SYSTEMS
# POLITECNICO DI MILANO

# Group: Recommendators
# 2016-2017
##############################################

#Import
import time
from time import strftime, clock
import os
import sys
import math
import string
import random
import numpy as np
import pandas as pd

#Data read
u_names = ['user_id','jobroles','career_level','discipline_id','industry_id','industry_id','country','region','experience_n_entries_class','experience_years_experience','experience_years_in_current','edu_degree','edu_fieldofstudies']

article_df = pd.read_table('Datasets/user_profile.csv',sep='  ',header=None,names=u_names).drop_duplicates()
#article_df = article_df.sort('user_id',ascending=True)#.groupby('article_id',as_index=False).first()

article_df.head(10)




#Data analysis


#Output



